using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UITesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UITest.Extension;
using Keyboard = Microsoft.VisualStudio.TestTools.UITesting.Keyboard;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UITesting.WpfControls;
using CodedUI.DebuggingHelpers;
using Microsoft.VisualStudio.TestTools.UITesting.WinControls;
using System.Threading;

namespace CodedUI.DebuggingHelpers
{
    [CodedUITest]
    public class FastTests
    {
        [TestMethod]
        public void Test()
        {
            var process = Process.GetProcessesByName("iexplore").FirstOrDefault();
            var app = ApplicationUnderTest.FromProcess(process);

            var loaded = LoadedUITestControl.LoadTree(app);
            Thread.Sleep(TimeSpan.FromHours(1));
        }
    }
}
