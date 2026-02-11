using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Database_Designer
{
    public sealed partial class App : Application
    {
        MainPage mainPage;
        public App()
        {
            this.InitializeComponent();
            mainPage = new MainPage();
            Window.Current.Content = mainPage;
        }

    }
}
