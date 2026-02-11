using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Database_Designer
{
    public partial class ProjectsView : Page
    {
        private MainPage mainPaged;
        public UIWindowEntry WindowInfo { get; private set; }

        public ProjectsView(MainPage mainpage)
        {
            this.InitializeComponent();
            mainPaged = mainpage;
            TableObjects = mainpage.MainSessionInfo.Tables;

            mainpage.IntroPage.Children.Remove(this);



                ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };

            

            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }


        public void RemoveWindow()
        {
            try
            {
                //if (WindowInfo == null) return;

                if (mainPaged?.LowerAppBar != null && WindowInfo.Shortcut != null)
                {
                    try
                    {
                        if (mainPaged.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                            mainPaged.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                        if (WindowInfo.Shortcut is FrameworkElement sh) sh.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }

                if (WindowInfo.Elements != null && mainPaged?.IntroPage != null)
                {
                    for (int i = WindowInfo.Elements.Count - 1; i >= 0; i--)
                    {
                        var item = WindowInfo.Elements[i];
                        try
                        {
                            if (item is FrameworkElement fe)
                            {
                                fe.Visibility = Visibility.Collapsed;
                                fe.DataContext = null;
                            }
                            if (mainPaged.IntroPage.Children.Contains(item))
                                mainPaged.IntroPage.Children.Remove(item);
                        }
                        catch { }
                    }
                    try { WindowInfo.Elements.Clear(); } catch { }
                }

                try
                {
                    if (this is FrameworkElement me) me.Visibility = Visibility.Collapsed;
                    if (mainPaged?.IntroPage != null && mainPaged.IntroPage.Children.Contains(this))
                        mainPaged.IntroPage.Children.Remove(this);
                }
                catch { }
            }
            catch { }

        }

        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private int ButtonPage = 0;
        private ObservableCollection<SessionStorage.TableObject> TableObjects;
        private List<SessionStorage.TableObject> FauxList;


        //12 a page

        public void LoadDatabaseByPage ()
        {

            if (ButtonPage <= -1)
            {
                ButtonPage = 0;
            }

            var startingInt = (12 * ButtonPage);

            FauxList = TableObjects.ToList();

            foreach (var item in FauxList)
            {

                FauxList.RemoveRange(0, startingInt);

                var remainder = FauxList.Count();

                if (remainder < 12)
                {

                }

                else
                {
                    FauxList.RemoveRange(11, remainder - 12);
                }
             
                //Add button enabling/disabling things later
            }
        }

        public void LoadDatabaseByString(string str)
        {

        }

        //Good for a fiu;ll db dearch, however I need to fix this to be good for smallr databases
        //For example, this would be good for the file system but is actually horribkle for the current system as is. It handles sessions, not tables

        public void CreateTableObject (string TableName, string Img, string Description)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            List<string> keys = new List<string>();
            StringBuilder creator = new StringBuilder();

            int i = 0;

            foreach (var item in TableObjects)
            {
                creator.Clear();
                creator.Append(item.TableName);
                creator.Append(item.Description);
                creator.Append(item.SchemaName);
                var searchString = creator.ToString();

                dict.Add(searchString, i);

                keys.Add(searchString);


                i++;
            }

            FauxList = FauxList.Where(n => keys.Contains("A")).ToList();

        }


    }
}
