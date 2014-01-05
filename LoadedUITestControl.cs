using Microsoft.VisualStudio.TestTools.UITest.Extension;
using Microsoft.VisualStudio.TestTools.UITesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CodedUI.DebuggingHelpers
{
    [DebuggerDisplay("{DebuggerDisplayValue,nq}", Type = "{DebuggerDisplayType,nq}")]
    public class LoadedUITestControl
    {
        #region Defaults/Constants

        private static int DefaultDepth()
        {
            return 2;
        }

        private static LoadingMechanism DefaultLoadingMechanism()
        {
            return LoadingMechanism.BlackList;
        }

        private static TimeSpan DefaultTimeout()
        {
            return TimeSpan.FromMilliseconds(200);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Type AncestorType = typeof(UITestControl);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        #endregion

        #region Internal classes

        public enum LoadingMechanism
        {
            BlackList, WhiteList
        }

        private class Configuration
        {
            public LoadingMechanism LoadingMechanism { get; set; }
            public List<string> BlackOrWhiteList { get; set; }
            public WaitFor<object> WaitFor { get; set; }
        }

        [DebuggerDisplay("{Value}", Name = "{Name,nq}", Type = "{Type.ToString(),nq}")]
        internal struct Property
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            internal string Name;

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal object Value;

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            internal Type Type;
            
            internal Property(string name, object value, Type type)
            {
                Name = name;
                Value = value;
                Type = type;
            }
        }

        #endregion

        #region Hidden members

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly UITestControl _source;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<LoadedUITestControl> _children;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Property[] _properties;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)] 
        private readonly Configuration _configuration;

        #endregion

        #region Debugger display properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplayValue
        {
            get
            {
                return _source.ToString();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplayType
        {
            get
            {
                return String.Format("{0} {{{1}}}", this.GetType(), _source.GetType());
            }
        }

        #endregion

        #region Constructor & LoadTree

        public static LoadedUITestControl LoadTree(UITestControl source, TimeSpan timeout, params string[] blackOrWhiteList)
        {
            return LoadTree(source, DefaultDepth(), timeout, DefaultLoadingMechanism(), blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source, LoadingMechanism loadingMechanism, params string[] blackOrWhiteList)
        {
            return LoadTree(source, DefaultDepth(), DefaultTimeout(), loadingMechanism, blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source, int maxDepth, params string[] blackOrWhiteList)
        {
            return LoadTree(source, maxDepth, DefaultTimeout(), DefaultLoadingMechanism(), blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source, params string[] blackOrWhiteList)
        {
            return LoadTree(source, DefaultDepth(), DefaultTimeout(), DefaultLoadingMechanism(), blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source, int maxDepth, TimeSpan timeout, LoadingMechanism loadingMechanism, params string[] blackOrWhiteList)
        {
            ValidateSourceIsBound(source);

            var configuration = new Configuration()
            {
                LoadingMechanism = loadingMechanism,
                BlackOrWhiteList = blackOrWhiteList.ToList(),
                WaitFor = new WaitFor<object>(timeout)
            };

            return new LoadedUITestControl(source, maxDepth, configuration);
        }

        private static void ValidateSourceIsBound(UITestControl source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            if (SourceIsBound(source)) return;

            try
            {
                Debugger.NotifyOfCrossThreadDependency();
                source.Find();
            }

            catch (UITestException ex)
            {
                throw new ArgumentException("source must be a live object. see inner excption for details", ex);
            }
        }

        private static bool SourceIsBound(UITestControl source)
        {
            var prop = AncestorType.GetProperty("IsBound", BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.Instance);
            return (bool)prop.GetValue(source);
        }

        private LoadedUITestControl(UITestControl source, int maxDepth, Configuration configuration)
        {
            _source = source;
            _configuration = configuration;

            LoadProperties();
            LoadChildren(maxDepth - 1);
        }

        #endregion

        #region Properties

        public List<LoadedUITestControl> Children 
        {
            get
            {
                if (_children == null)
                {
                    LoadChildren();
                }
                return _children;
            } 
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        internal Property[] Z_Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = LoadDynamicProperties();        
                }
                return _properties;
            }
        }

        public string ClassName { get; private set; }
        public ControlType ControlType { get; private set; }
        public string FriendlyName { get; private set; }
        public string Name { get; private set; }

        #endregion

        #region Loaders

        private void LoadChildren(int maxDepth = 1)
        {
            if (maxDepth < 1) return;

            Debugger.NotifyOfCrossThreadDependency();
            _children = new List<LoadedUITestControl>(from child in _source.GetChildren()
                                                      select new LoadedUITestControl(child, maxDepth, _configuration));
        }

        private void LoadProperties()
        {
            Debugger.NotifyOfCrossThreadDependency();

            LoadUITestControlProperties();
            _properties = LoadDynamicProperties();        
        }

        private void LoadUITestControlProperties()
        {
            ControlType = _source.ControlType;
            FriendlyName = _source.FriendlyName;
            Name = _source.Name;
            ClassName = _source.ClassName;
        }

        private Property[] LoadDynamicProperties()
        {
            if (_source == null)
            {
                return new Property[]{};
            }

            var properties = new List<Property>();
            var type = _source.GetType();

            var props = from prop in type.GetProperties(Flags)
                        where ToLoadProperty(prop)
                        select prop;

            foreach (var prop in props)
            {
                object value = null;
                try
                {
                    value = _configuration.WaitFor.Run(() => prop.GetValue(_source, null));
                }

                catch (TimeoutException)
                {
                    IgnoreProperty(prop);
                }

                catch (Exception ex)
                {
                    value = ex;
                }

                if (value != null)
                {
                    properties.Add(new Property(prop.Name, value, prop.PropertyType));
                }
            }

            return properties.ToArray();
        }

        private bool ToLoadProperty(PropertyInfo prop)
        {
            if (prop.DeclaringType == AncestorType ||
                prop.PropertyType.IsSubclassOf(AncestorType) ||
                prop.CanRead == false)
            {
                return false;
            }

            switch (_configuration.LoadingMechanism)
            {
                case LoadingMechanism.BlackList:
                    return !_configuration.BlackOrWhiteList.Contains(prop.Name);

                case LoadingMechanism.WhiteList:
                    return _configuration.BlackOrWhiteList.Contains(prop.Name);

                default: return false;
            }
        }

        private void IgnoreProperty(PropertyInfo prop)
        {
            switch (_configuration.LoadingMechanism)
            {
                case LoadingMechanism.BlackList:
                    _configuration.BlackOrWhiteList.Add(prop.Name);
                    break;

                case LoadingMechanism.WhiteList:
                    _configuration.BlackOrWhiteList.RemoveAll(s => s.Equals(prop.Name));
                    break;
            }
        }

        #endregion
    }
}
