using System;
using System.Windows;

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