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
        #region Constants

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const int DEFAULT_DEPTH = 3;

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
        private List<string> _blackOrWhiteList;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private LoadingMechanism _loadingMechanism;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        WaitFor<object> _waitFor;

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

        public static LoadedUITestControl LoadTree(UITestControl source, LoadingMechanism loadingMechanism, params string[] blackOrWhiteList)
        {
            return LoadTree(source, DEFAULT_DEPTH, loadingMechanism, blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source,int maxDepth, params string[] blackOrWhiteList)
        {
            return LoadTree(source, maxDepth, LoadingMechanism.BlackList, blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source, params string[] blackOrWhiteList)
        {
            return LoadTree(source, DEFAULT_DEPTH, LoadingMechanism.BlackList, blackOrWhiteList);
        }

        public static LoadedUITestControl LoadTree(UITestControl source, int maxDepth, LoadingMechanism loadingMechanism, params string[] blackOrWhiteList)
        {
            return new LoadedUITestControl(source, maxDepth, new WaitFor<object>(TimeSpan.FromMilliseconds(200)), loadingMechanism, blackOrWhiteList.ToList());
        }

        private LoadedUITestControl(UITestControl source, int maxDepth, WaitFor<object> waitFor, LoadingMechanism loadingMechanism, List<string> blackOrWhiteList)
        {
            _source = source;
            _blackOrWhiteList = blackOrWhiteList ?? new List<string>();
            _loadingMechanism = loadingMechanism;
            _waitFor = waitFor;

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
                                                      select new LoadedUITestControl(child, maxDepth, _waitFor, _loadingMechanism, _blackOrWhiteList));
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
                    value = _waitFor.Run(() => prop.GetValue(_source, null));
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

            switch (_loadingMechanism)
            {
                case LoadingMechanism.BlackList:
                    return !_blackOrWhiteList.Contains(prop.Name);

                case LoadingMechanism.WhiteList:
                    return _blackOrWhiteList.Contains(prop.Name);

                default: return false;
            }
        }

        private void IgnoreProperty(PropertyInfo prop)
        {
            switch (_loadingMechanism)
            {
                case LoadingMechanism.BlackList:
                    _blackOrWhiteList.Add(prop.Name);
                    break;

                case LoadingMechanism.WhiteList:
                    _blackOrWhiteList.RemoveAll(s => s.Equals(prop.Name));
                    break;
            }
        }

        #endregion
    }
}
