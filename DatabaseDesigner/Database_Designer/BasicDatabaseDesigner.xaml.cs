using DatabaseDesigner;
using Org.BouncyCastle.Bcpg;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;
using WISecureData;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;
using static OpenSilver.Features;


namespace Database_Designer
{
    public partial class BasicDatabaseDesigner : Page
    {
        static string baseUrl;

        public enum DesignMode
        {
            Design,
            Index,
            Ref,
            Project
        }

        public UIWindowEntry WindowInfo { get; private set; }

        DatabaseViewer? DBViewer;
        public BasicDatabaseDesigner(MainPage mainPaged, DatabaseViewer viewer, Projects? proj, DesignMode mode, SessionStorage.RowCreation? row, SessionStorage.ReferenceOptions? reference, SessionStorage.IndexCreation? index, SessionStorage.TableObject data = new TableObject())
        {
            this.InitializeComponent();
            
            baseUrl = mainPaged.baseUrl;

            ClosePage.Click += (s, e) =>
            {
                RemoveWindow() ; //Fuh it brute force what can go wrong
            };

            mainPage = mainPaged;
            projectPage = proj;
            DBViewer = viewer;


            switch (mode)
            {
                case DesignMode.Design:
                    CreateDesign(data, row);
                    break;
                case DesignMode.Index:
                        CreateIndex(data, index);                
                    break;
                case DesignMode.Ref:
                    CreateReference(data, reference);
                    break;


                case DesignMode.Project:

                    CreateProject();

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }


            ExitButton.Click += (s, e) => RemoveWindow();



            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }

        private bool _removed = false;

        public void RemoveWindow()
        {
            if (_removed)
                return;
            _removed = true;

            try
            {
                //if (WindowInfo == null) return;

                if (mainPage?.LowerAppBar != null && WindowInfo.Shortcut != null)
                {
                    try
                    {
                        if (mainPage.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                            mainPage.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                        if (WindowInfo.Shortcut is FrameworkElement sh) sh.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }

                if (WindowInfo.Elements != null && mainPage?.IntroPage != null)
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
                            if (mainPage.IntroPage.Children.Contains(item))
                                mainPage.IntroPage.Children.Remove(item);
                        }
                        catch { }
                    }
                    try { WindowInfo.Elements.Clear(); } catch { }
                }

                try
                {
                    if (this is FrameworkElement me) me.Visibility = Visibility.Collapsed;
                    if (mainPage?.IntroPage != null && mainPage.IntroPage.Children.Contains(this))
                        mainPage.IntroPage.Children.Remove(this);
                }
                catch { }
            }
            catch { }
        }



        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }


        //Let's use DB Designer's terminal to create logic for creating and managing a DB

        internal MainPage mainPage;
        private Projects projectPage;

        string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.()-=+,:;/\\!?@[]{} ";



        private List<Button> _buttons = new List<Button>();
        private Color _defaultColor = Color.FromArgb(255, 69, 65, 56);
        private Color _highlightColor = Color.FromArgb(255, 100, 96, 86);

        // This holds all the buttons in a horizontal stack


        public void CreateDesign(SessionStorage.TableObject refTable, SessionStorage.RowCreation? sessionItem)
        {

            //Five sections; Basics, Data Type, Array, Primary/Unique/NotNull, Default Value, Value Check, Finalize.

            Tabs.Items.Clear();

            if (sessionItem == null)
            {
                sessionItem = new SessionStorage.RowCreation();
            }

            var sessionData = (SessionStorage.RowCreation)sessionItem;

            bool originalIsNull = sessionItem == null;

            var sessionDataHistory = sessionData;


            //Creates the basic tabs area

            #region Basic Setup

            var BasicsTab = new TabItem
            {
                Header = "Basics",
            };

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            BasicsTab.Content = contentPanel;

            var Title = UIHelpers.CreateBasicTitle("Let's Set The Basics For This Row");
            var Subtitle = UIHelpers.CreateSubTitle("You can use uppercase and lowercase letters, numbers and the following symbols; _.()-=+,:;/\\!?@[]{}.");
            var SubtitleB = UIHelpers.CreateSubTitle("You must click Validate to update the table when you see it; else it's updated automatically.");
            contentPanel.Children.Add(Title);
            contentPanel.Children.Add(Subtitle);
            contentPanel.Children.Add(SubtitleB);

            #endregion

            #region Row Name and Description Logic

            var NameOfRow = UIHelpers.CreateTextInput("To begin, what's the name of this row?");
            var RowDescription = UIHelpers.CreateTextInput("Please enter a description!");
            var ErrorText = UIHelpers.CreateError();

            contentPanel.Children.Add(NameOfRow.Item2);
            contentPanel.Children.Add(RowDescription.Item2);
            contentPanel.Children.Add(ErrorText);

            Tabs.Items.Add(BasicsTab);




            if (sessionItem == null)
            {
                NameOfRow.Item1.Text = "";
                RowDescription.Item1.Text = "";
            }

            else
            {
                NameOfRow.Item1.Text = sessionData.Name;
                RowDescription.Item1.Text = sessionData.Description;
            }


            NameOfRow.Item1.TextChanged += (s, e) =>
            {
                ValidateTitleAndSubtitle(s,e);
            };

            RowDescription.Item1.TextChanged += (s, e) =>
            {
                ValidateTitleAndSubtitle(s, e);
            };


            void ValidateTitleAndSubtitle(object sender, RoutedEventArgs e)
            {
                UIHelpers.ResetError(ErrorText);

                var fieldName = NameOfRow.Item1.Text;
                var description = RowDescription.Item1.Text;


                // Check if a row with the same name already exists


                bool nameExists = refTable.Rows.Any(r =>
                    string.Equals(r.Name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                    r.GetHashCode() != sessionDataHistory.GetHashCode()  
                );
                if (nameExists)
                {
                    UIHelpers.SetError(ErrorText,
                        $"Please enter a valid name for the row; ensure it hasn't been used already. Entered text: {fieldName}");
                    return;
                }


                if (string.IsNullOrWhiteSpace(description)
                    || !description.All(c => AllowedChars.Contains(c)))
                {
                    UIHelpers.SetError(ErrorText, "Please enter a valid description for the row.");
                    return;
                }

                sessionData.Name = UIHelpers.ReadLineWithUnderscores(fieldName);
                sessionData.Description = UIHelpers.ReadLineWithUnderscores(description);


            }






            #endregion


            #region Basic Setup

            var DataTypesTab = new TabItem
            {
                Header = "DataType",
            };

            var containerGrid = new Grid();
            containerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var contentPanel2 = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,

                Background = new SolidColorBrush(Colors.Transparent)
            };


            var scrollViewerForDataTypes = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanel2,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270, 
                Height = 808
            };

            scrollViewerForDataTypes.MouseWheel += (sender, e) =>
            {

                double newOffset = scrollViewerForDataTypes.VerticalOffset - e.Delta;
                scrollViewerForDataTypes.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };

            var scrollbarStyle = new Style(typeof(ScrollBar));
            scrollbarStyle.Setters.Add(new Setter(ScrollBar.WidthProperty, 8.0)); 
            scrollViewerForDataTypes.Resources.Add(typeof(ScrollBar), scrollbarStyle);

            Grid.SetRow(scrollViewerForDataTypes, 0);
            containerGrid.Children.Add(scrollViewerForDataTypes);

            #endregion

            #region Main Logic

            DataTypesTab.Content = containerGrid;
            Tabs.Items.Add(DataTypesTab);




            var dataTypeTitle = UIHelpers.CreateBasicTitle("What Data Type Is This?");
            contentPanel2.Children.Add(dataTypeTitle);





            var encryptionQuestion = UIHelpers.CreateSubTitle("Is this row encrypted and NOT media?");
            contentPanel2.Children.Add(encryptionQuestion);

            var encryptedNotMediaYesButton = UIHelpers.CreateBasicButton("Yes");
            encryptedNotMediaYesButton.Item1.Click += OnEncryptedNotMediaYes;

            encryptedNotMediaYesButton.Item1.Click += (s, e) =>
            {
                sessionData.EncryptedAndNOTMedia = true;
                sessionData.Media = false;
                sessionData.RowType = PostgresType.Text;
            };

            contentPanel2.Children.Add(encryptedNotMediaYesButton.Item2);

            var encryptedNotMediaNoButton = UIHelpers.CreateBasicButton("No");
            encryptedNotMediaNoButton.Item1.Click += OnEncryptedNotMediaNo;
            contentPanel2.Children.Add(encryptedNotMediaNoButton.Item2);


            var encNotMediaButtonList = new List<Button>();

            encNotMediaButtonList.Add(encryptedNotMediaYesButton.Item1);
            encNotMediaButtonList.Add(encryptedNotMediaNoButton.Item1);


            if (sessionData.EncryptedAndNOTMedia != null)
            {
                if (sessionData.EncryptedAndNOTMedia == true)
                {
                    UIHelpers.EnableHighlighting(encNotMediaButtonList, encryptedNotMediaYesButton.Item1);

                }
                else
                {
                    UIHelpers.EnableHighlighting(encNotMediaButtonList, encryptedNotMediaNoButton.Item1);

                }
            }

            else
            {
                UIHelpers.EnableHighlighting(encNotMediaButtonList);
            }

            if (sessionData.Media != null)
            {
                if (sessionData.Media == true)
                {
                    //On Encrypted Media No

                    var mediaQuestion = UIHelpers.CreateSubTitle("Is this row media?");
                    contentPanel2.Children.Add(mediaQuestion);

                    var isMediaYesButton = UIHelpers.CreateBasicButton("Yes");
                    isMediaYesButton.Item1.Click += OnIsMediaYes;
                    contentPanel2.Children.Add(isMediaYesButton.Item2);

                    isMediaYesButton.Item1.Click += (s, e) =>
                    {
                        sessionData.EncryptedAndNOTMedia = false;
                        sessionData.Media = true;
                        sessionData.RowType = PostgresType.SecureMedia;
                    };

                    var isMediaNoButton = UIHelpers.CreateBasicButton("No");
                    isMediaNoButton.Item1.Click += OnIsMediaNo;
                    contentPanel2.Children.Add(isMediaNoButton.Item2);

                    var mediaYesButtonList = new List<Button>();

                    mediaYesButtonList.Add(isMediaYesButton.Item1);
                    mediaYesButtonList.Add(isMediaNoButton.Item1);

                    UIHelpers.EnableHighlighting(mediaYesButtonList, isMediaYesButton.Item1);


                }
                else if (sessionData.Media == false)
                {
                    var mediaQuestion = UIHelpers.CreateSubTitle("Is this row media?");
                    contentPanel2.Children.Add(mediaQuestion);

                    var isMediaYesButton = UIHelpers.CreateBasicButton("Yes");
                    isMediaYesButton.Item1.Click += OnIsMediaYes;
                    contentPanel2.Children.Add(isMediaYesButton.Item2);

                    isMediaYesButton.Item1.Click += (s, e) =>
                    {
                        sessionData.EncryptedAndNOTMedia = false;
                        sessionData.Media = true;
                        sessionData.RowType = PostgresType.SecureMedia;
                    };

                    var isMediaNoButton = UIHelpers.CreateBasicButton("No");
                    isMediaNoButton.Item1.Click += OnIsMediaNo;
                    contentPanel2.Children.Add(isMediaNoButton.Item2);

                    var mediaYesButtonList = new List<Button>();

                    mediaYesButtonList.Add(isMediaYesButton.Item1);
                    mediaYesButtonList.Add(isMediaNoButton.Item1);

                    UIHelpers.EnableHighlighting(mediaYesButtonList, isMediaNoButton.Item1);


                }
            }

            if (sessionData.RowType != null)
            {


                var inputPrompt = UIHelpers.CreateSubTitle("What kind of row is this?");
                contentPanel2.Children.Add(inputPrompt);

                var mediaYesButtonListTemp = new List<Button>();
                Button selectedButton = default;

                foreach (var item in PostgresTypeDescriptions)
                {
                    var buttonTuple = UIHelpers.CreateBasicButton($"{item.Key} - {item.Value}");


                    mediaYesButtonListTemp.Add(buttonTuple.Item1);
                    var button = buttonTuple.Item1;

                    string typeKey = item.Key;

                    button.Click += async (s, args) =>
                    {
                        await Task.Yield();

                        if (Enum.TryParse<DBDesigner.PostgresType>(typeKey, out var parsedType))
                        {
                            sessionData.EncryptedAndNOTMedia = false;
                            sessionData.Media = false;
                            sessionData.RowType = parsedType;

                         

                        }


                        else
                        {
                            Console.WriteLine($"Failed to parse {typeKey} into PostgresType enum.");
                        }
                    };

                   // button.MouseRightButtonDown += (s, args) =>
       

                        var valType = Enum.TryParse<DBDesigner.PostgresType>(typeKey, out var parsedType);

                    if (parsedType == sessionData.RowType)
                    {
                        selectedButton = button;
                    }

                    contentPanel2.Children.Add(buttonTuple.Item2);
                }

                UIHelpers.EnableHighlighting(mediaYesButtonListTemp, selectedButton);




            }

            





            void OnEncryptedNotMediaYes(object sender, RoutedEventArgs e)
            {
                UIHelpers.TrimStackPanel(contentPanel2, 4);
            }

            void OnEncryptedNotMediaNo(object sender, RoutedEventArgs e)
            {
                var mediaQuestion = UIHelpers.CreateSubTitle("Is this row media?");
                contentPanel2.Children.Add(mediaQuestion);

                var isMediaYesButton = UIHelpers.CreateBasicButton("Yes");
                isMediaYesButton.Item1.Click += OnIsMediaYes;
                contentPanel2.Children.Add(isMediaYesButton.Item2);

                isMediaYesButton.Item1.Click += (s, e) =>
                {
                    sessionData.EncryptedAndNOTMedia = false;
                    sessionData.Media = true;
                    sessionData.RowType = PostgresType.SecureMedia;
                };

                var isMediaNoButton = UIHelpers.CreateBasicButton("No");
                isMediaNoButton.Item1.Click += OnIsMediaNo;
                contentPanel2.Children.Add(isMediaNoButton.Item2);

                var mediaYesButtonList = new List<Button>();

                mediaYesButtonList.Add(isMediaYesButton.Item1);
                mediaYesButtonList.Add(isMediaNoButton.Item1);

                UIHelpers.EnableHighlighting(mediaYesButtonList);


            }

            void OnIsMediaYes(object sender, RoutedEventArgs e)
            {
                UIHelpers.TrimStackPanel(contentPanel2, 7);
            }

            void OnIsMediaNo(object sender, RoutedEventArgs e)
            {
                var inputPrompt = UIHelpers.CreateSubTitle("What kind of row is this?");
                contentPanel2.Children.Add(inputPrompt);

                var mediaYesButtonList = new List<Button>();


                foreach (var item in PostgresTypeDescriptions)
                {
                    var buttonTuple = UIHelpers.CreateBasicButton($"{item.Key} - {item.Value}");
                    mediaYesButtonList.Add(buttonTuple.Item1);
                    var button = buttonTuple.Item1;

                    string typeKey = item.Key; 

                    button.Click += async (s, args) =>
                    {
                        await Task.Yield();

                        if (Enum.TryParse<DBDesigner.PostgresType>(typeKey, out var parsedType))
                        {
                            sessionData.EncryptedAndNOTMedia = false;
                            sessionData.Media = false;
                            sessionData.RowType = parsedType;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to parse {typeKey} into PostgresType enum.");
                        }
                    };

                    contentPanel2.Children.Add(buttonTuple.Item2);
                }

                UIHelpers.EnableHighlighting(mediaYesButtonList);


            }







            #endregion


            #region Limits and Arrays

            var LimitsnArrayTab = new TabItem
            {
                Header = "Limits & Arrays",
            };

            var contentPanel3 = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerForLimits = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanel3,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerForLimits.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerForLimits.VerticalOffset - e.Delta;
                scrollViewerForLimits.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };


            LimitsnArrayTab.Content = scrollViewerForLimits;
            Tabs.Items.Add(LimitsnArrayTab);

            // UI Elements
            var TitleLimitsnArray = UIHelpers.CreateBasicTitle("What Kind Of Limits Are We Setting?");

            bool SupportsLimit(PostgresType type)
            {
                return type == PostgresType.VarChar ||
                       type == PostgresType.Char ||
                       type == PostgresType.Text ||
                       type == PostgresType.Time ||
                       type == PostgresType.TimeTz ||
                       type == PostgresType.Timestamp ||
                       type == PostgresType.TimestampTz ||
                       type == PostgresType.Numeric ||
                       type == PostgresType.Money;
            }

            bool TryValidateLimitString(PostgresType type, string? limitValue, out string? error)
            {
                error = null;

                if (!SupportsLimit(type))
                {
                    error = $"Type {type} does not support limit values.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(limitValue))
                {
                    error = "Limit value cannot be empty.";
                    return false;
                }

                limitValue = limitValue.Trim();

                if (type == PostgresType.Numeric || type == PostgresType.Money)
                {
                    // Expect format like "precision,scale"
                    var parts = limitValue.Split(',');
                    if (parts.Length != 2 ||
                        !int.TryParse(parts[0], out int precision) ||
                        !int.TryParse(parts[1], out int scale) ||
                        precision <= 0 || scale < 0 || scale > precision)
                    {
                        error = "Expected format: precision,scale (e.g., 10,2) where scale ≤ precision.";
                        return false;
                    }
                    return true;
                }

                if (!int.TryParse(limitValue, out int singleLimit) || singleLimit <= 0)
                {
                    error = "Expected a positive integer value (e.g., 255). Please try again.";
                    return false;
                }

                return true;
            }


            bool IsValidNumberString(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Remove underscores if you want to allow digit grouping (like 1_000)
            var sanitized = input.Replace("_", "");

            // Try parse as double to cover integer and decimal numbers
            return double.TryParse(sanitized,
                                   System.Globalization.NumberStyles.AllowLeadingSign
                                   | System.Globalization.NumberStyles.AllowDecimalPoint
                                   | System.Globalization.NumberStyles.AllowExponent,
                                   System.Globalization.CultureInfo.InvariantCulture,
                                   out _);
        }



            var scalarLimitTitle = UIHelpers.CreateSubTitle("Do variables in this row have a length limit?");

            var scalarLimitYes = UIHelpers.CreateBasicButton("Yes");
            var scalarLimitNo = UIHelpers.CreateBasicButton("No");
            var scalarLimitInput = UIHelpers.CreateTextInput("Please enter the length limit for the variable (Number only).");
            var scalarCheck = UIHelpers.CreateBasicButton("Validate Length");
            var scalarError = UIHelpers.CreateError();

            var isArrayTitle = UIHelpers.CreateSubTitle("Is this row an array?");
            var isArrayYes = UIHelpers.CreateBasicButton("Yes");
            var isArrayNo = UIHelpers.CreateBasicButton("No");

            var arrayLimitTitle = UIHelpers.CreateSubTitle("Does this array have a length limit?");
            var arrayLimitYes = UIHelpers.CreateBasicButton("Yes");
            var arrayLimitNo = UIHelpers.CreateBasicButton("No");
            var arrayLimitInput = UIHelpers.CreateTextInput("Please enter the length limit for the array (Number only).");
            var arrayLimitCheck = UIHelpers.CreateBasicButton("Validate Length");
            var arrayLimitError = UIHelpers.CreateError();

            scalarCheck.Item2.Visibility = Visibility.Collapsed;
            arrayLimitCheck.Item2.Visibility = Visibility.Collapsed;

            // Only show scalar limit if type supports ()
            bool typeSupportsLimit = sessionData.RowType == PostgresType.Char ||
                                     sessionData.RowType == PostgresType.VarChar ||
                                     sessionData.RowType == PostgresType.Numeric ||
                                     sessionData.RowType == PostgresType.Time ||
                                     sessionData.RowType == PostgresType.Timestamp;

            scalarLimitTitle.Visibility = typeSupportsLimit ? Visibility.Visible : Visibility.Collapsed;
            scalarLimitYes.Item2.Visibility = typeSupportsLimit ? Visibility.Visible : Visibility.Collapsed;
            scalarLimitNo.Item2.Visibility = typeSupportsLimit ? Visibility.Visible : Visibility.Collapsed;
            scalarLimitInput.Item2.Visibility = Visibility.Collapsed; // hidden until user clicks Yes
            scalarCheck.Item2.Visibility = Visibility.Collapsed;
            scalarError.Visibility = Visibility.Collapsed;

            // Add controls to the scrollable panel
            contentPanel3.Children.Add(TitleLimitsnArray);

            contentPanel3.Children.Add(scalarLimitTitle);
            contentPanel3.Children.Add(scalarLimitYes.Item2);
            contentPanel3.Children.Add(scalarLimitNo.Item2);
            contentPanel3.Children.Add(scalarLimitInput.Item2);
            contentPanel3.Children.Add(scalarCheck.Item2);
            contentPanel3.Children.Add(scalarError);

            contentPanel3.Children.Add(isArrayTitle);
            contentPanel3.Children.Add(isArrayYes.Item2);
            contentPanel3.Children.Add(isArrayNo.Item2);

            contentPanel3.Children.Add(arrayLimitTitle);
            contentPanel3.Children.Add(arrayLimitYes.Item2);
            contentPanel3.Children.Add(arrayLimitNo.Item2);
            contentPanel3.Children.Add(arrayLimitInput.Item2);
            contentPanel3.Children.Add(arrayLimitCheck.Item2);
            contentPanel3.Children.Add(arrayLimitError);

            void ValidateArrayLimit(object sender, RoutedEventArgs e)
            {
                UIHelpers.ResetError(arrayLimitError);

                var input = arrayLimitInput.Item1.Text;

                if (string.IsNullOrWhiteSpace(input) || !IsValidNumberString(input))
                {
                    UIHelpers.SetError(arrayLimitError, $"Please enter a valid numeric limit value. Entered: '{input}'");
                    return;
                }

                sessionData.IsArray = true;
                sessionData.ArrayLimit = input;
            }

            void ValidateScalarLimit(object sender, RoutedEventArgs e)
            {
                if (!typeSupportsLimit) return; // safeguard

                UIHelpers.ResetError(scalarError);

                var input = scalarLimitInput.Item1.Text;

                if (!TryValidateLimitString((PostgresType)sessionData.RowType, input, out var error))
                {
                    UIHelpers.SetError(scalarError, $"Invalid scalar limit: {error}");
                    return;
                }

                sessionData.Limit = Row.LimitEncoder.Encode(input);
            }

            var scalarButtonList = new List<Button> { scalarLimitYes.Item1, scalarLimitNo.Item1 };
            UIHelpers.EnableHighlighting(scalarButtonList);

            var isArrayButtonList = new List<Button> { isArrayYes.Item1, isArrayNo.Item1 };
            var arrayLimitedButtonList = new List<Button> { arrayLimitYes.Item1, arrayLimitNo.Item1 };

            isArrayYes.Item1.Click += (s, e) =>
            {
                sessionData.IsNotNull = true;
                UIHelpers.EnableHighlighting(isArrayButtonList, isArrayYes.Item1);
            };

            isArrayNo.Item1.Click += (s, e) =>
            {
                sessionData.IsNotNull = false;
                UIHelpers.EnableHighlighting(isArrayButtonList, isArrayNo.Item1);
            };

            arrayLimitYes.Item1.Click += (s, e) =>
            {
                sessionData.IsUnique = true;
                UIHelpers.EnableHighlighting(arrayLimitedButtonList, arrayLimitYes.Item1);
            };

            arrayLimitNo.Item1.Click += (s, e) =>
            {
                sessionData.IsUnique = false;
                UIHelpers.EnableHighlighting(arrayLimitedButtonList, arrayLimitNo.Item1);
            };

            // Handle existing sessionData
            if (sessionData.IsArray != null)
            {
                if ((bool)sessionData.IsArray)
                {
                    UIHelpers.EnableHighlighting(isArrayButtonList, isArrayYes.Item1);
                    arrayLimitTitle.Visibility = Visibility.Visible;
                    arrayLimitYes.Item2.Visibility = Visibility.Visible;
                    arrayLimitNo.Item2.Visibility = Visibility.Visible;

                    if (sessionData.ArrayLimit != null)
                    {
                        UIHelpers.EnableHighlighting(arrayLimitedButtonList, arrayLimitYes.Item1);

                        arrayLimitInput.Item2.Visibility = Visibility.Visible;
                        arrayLimitCheck.Item2.Visibility = Visibility.Visible;
                        scalarLimitInput.Item1.Text = sessionData.ArrayLimit;
                    }
                    else
                    {
                        UIHelpers.EnableHighlighting(arrayLimitedButtonList, arrayLimitNo.Item1);
                    }
                }
                else
                {
                    UIHelpers.EnableHighlighting(isArrayButtonList, isArrayNo.Item1);
                }
            }

            scalarCheck.Item1.Click += ValidateScalarLimit;
            arrayLimitCheck.Item1.Click += ValidateArrayLimit;

            // Events
            scalarLimitYes.Item1.Click += (s, e) =>
            {
                if (!typeSupportsLimit) return; // hide if type doesn't support ()
                scalarLimitInput.Item2.Visibility = Visibility.Visible;
                scalarCheck.Item2.Visibility = Visibility.Visible;
            };

            scalarLimitNo.Item1.Click += (s, e) =>
            {
                scalarLimitInput.Item2.Visibility = Visibility.Collapsed;
                scalarCheck.Item2.Visibility = Visibility.Collapsed;
                sessionData.Limit = null;
                scalarLimitInput.Item1.Text = "";
                sessionData.ArrayLimit = null;
            };

            isArrayYes.Item1.Click += (s, e) =>
            {
                arrayLimitTitle.Visibility = Visibility.Visible;
                arrayLimitYes.Item2.Visibility = Visibility.Visible;
                arrayLimitNo.Item2.Visibility = Visibility.Visible;

                sessionData.IsArray = true;

                UIHelpers.ResetHighlight(arrayLimitedButtonList);
            };

            isArrayNo.Item1.Click += (s, e) =>
            {
                arrayLimitTitle.Visibility = Visibility.Collapsed;
                arrayLimitYes.Item2.Visibility = Visibility.Collapsed;
                arrayLimitNo.Item2.Visibility = Visibility.Collapsed;
                arrayLimitInput.Item2.Visibility = Visibility.Collapsed;
                arrayLimitCheck.Item2.Visibility = Visibility.Collapsed;
                sessionData.IsArray = false;
                sessionData.ArrayLimit = null;
                scalarLimitInput.Item1.Text = "";
            };

            arrayLimitYes.Item1.Click += (s, e) =>
            {
                arrayLimitInput.Item2.Visibility = Visibility.Visible;
                arrayLimitCheck.Item2.Visibility = Visibility.Visible;
                scalarLimitInput.Item1.Text = "";
            };

            arrayLimitNo.Item1.Click += (s, e) =>
            {
                arrayLimitInput.Item2.Visibility = Visibility.Collapsed;
                arrayLimitCheck.Item2.Visibility = Visibility.Collapsed;
                scalarLimitInput.Item1.Text = "";
            };



            // Fix This
            Tabs.SelectionChanged += (sender, args) =>
            {

                if (LimitsnArrayTab.IsSelected)
                {
                    // Only show scalar limit controls if the type supports limits
                    bool showScalarControls = sessionData.RowType != null &&
                                            SupportsLimit((PostgresType)sessionData.RowType);

                    scalarLimitTitle.Visibility = showScalarControls ? Visibility.Visible : Visibility.Collapsed;
                    scalarLimitYes.Item2.Visibility = showScalarControls ? Visibility.Visible : Visibility.Collapsed;
                    scalarLimitNo.Item2.Visibility = showScalarControls ? Visibility.Visible : Visibility.Collapsed;

                    UIHelpers.ResetError(scalarError);

                    if (showScalarControls)
                    {
                        if (sessionData.Limit != null)
                        {
                            UIHelpers.EnableHighlighting(scalarButtonList, scalarLimitYes.Item1);
                            scalarLimitInput.Item2.Visibility = Visibility.Visible;
                            scalarCheck.Item2.Visibility = Visibility.Visible;
                            scalarLimitInput.Item1.Text = sessionData.Limit.ToString();
                        }
                        else
                        {
                            UIHelpers.EnableHighlighting(scalarButtonList, scalarLimitNo.Item1);
                            scalarLimitInput.Item2.Visibility = Visibility.Collapsed;
                            scalarCheck.Item2.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else
                {
                    scalarLimitTitle.Visibility = Visibility.Collapsed;
                    scalarLimitYes.Item2.Visibility = Visibility.Collapsed;
                    scalarLimitNo.Item2.Visibility = Visibility.Collapsed;
                    scalarLimitInput.Item2.Visibility = Visibility.Collapsed;
                    scalarCheck.Item2.Visibility = Visibility.Collapsed;
                    UIHelpers.ResetError(scalarError);
                }
            };



            #endregion

            #region Primary Key Logic

            var primaryKeyTab = new TabItem
            {
                Header = "Primary Key",
            };

            var contentPanelPK = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerForPK = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanelPK,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerForPK.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerForPK.VerticalOffset - e.Delta;
                scrollViewerForPK.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };

            primaryKeyTab.Content = scrollViewerForPK;
            Tabs.Items.Add(primaryKeyTab);

            // UI Elements
            var titlePK = UIHelpers.CreateBasicTitle("What Are We Defining About This Row?");
            var SelectionD = UIHelpers.CreateSubTitle("");


            var isPrimaryTitle = UIHelpers.CreateSubTitle("Is this row a primary key? This should be a unique identifier.");
            var isPrimaryYes = UIHelpers.CreateBasicButton("Yes");
            var isPrimaryNo = UIHelpers.CreateBasicButton("No");

            var isNotNullTitle = UIHelpers.CreateSubTitle("Is this row required to have a value?");
            var isNotNullYes = UIHelpers.CreateBasicButton("Yes");
            var isNotNullNo = UIHelpers.CreateBasicButton("No");

            var isRowUnique = UIHelpers.CreateSubTitle("Is this row unique?");
            var isUniqueYes = UIHelpers.CreateBasicButton("Yes");
            var isUniqueNo = UIHelpers.CreateBasicButton("No");

            var isUniqueButtonList = new List<Button>();

            isUniqueButtonList.Add(isUniqueYes.Item1);
            isUniqueButtonList.Add(isUniqueNo.Item1);

            var isPrimaryButtonList = new List<Button>();

            isPrimaryButtonList.Add(isPrimaryYes.Item1);
            isPrimaryButtonList.Add(isPrimaryNo.Item1);

            var isNotNullButtonList = new List<Button>();

            isNotNullButtonList.Add(isNotNullYes.Item1);
            isNotNullButtonList.Add(isNotNullNo.Item1);



            isNotNullYes.Item1.Click += (s, e) =>
            {
                sessionData.IsNotNull = true;
                UIHelpers.EnableHighlighting(isNotNullButtonList, isNotNullYes.Item1);

            };

            isNotNullNo.Item1.Click += (s, e) =>
            {
                sessionData.IsNotNull = false;
                UIHelpers.EnableHighlighting(isNotNullButtonList, isNotNullNo.Item1);

            };


            isUniqueYes.Item1.Click += (s, e) =>
            {
                sessionData.IsUnique = true;
                UIHelpers.EnableHighlighting(isUniqueButtonList, isUniqueYes.Item1);

            };

            isUniqueNo.Item1.Click += (s, e) =>
            {
                sessionData.IsUnique = false;
                UIHelpers.EnableHighlighting(isUniqueButtonList, isUniqueNo.Item1);

            };






            // Add to UI
            contentPanelPK.Children.Add(titlePK);
            contentPanelPK.Children.Add(SelectionD);

            contentPanelPK.Children.Add(isPrimaryTitle);
            contentPanelPK.Children.Add(isPrimaryYes.Item2);
            contentPanelPK.Children.Add(isPrimaryNo.Item2);
            contentPanelPK.Children.Add(isNotNullTitle);
            contentPanelPK.Children.Add(isNotNullYes.Item2);
            contentPanelPK.Children.Add(isNotNullNo.Item2);
            contentPanelPK.Children.Add(isRowUnique);
            contentPanelPK.Children.Add(isUniqueYes.Item2);
            contentPanelPK.Children.Add(isUniqueNo.Item2);

            // Initial visibility
            isNotNullTitle.Visibility = Visibility.Collapsed;
            isNotNullYes.Item2.Visibility = Visibility.Collapsed;
            isNotNullNo.Item2.Visibility = Visibility.Collapsed;
            isRowUnique.Visibility = Visibility.Collapsed;
            isUniqueYes.Item2.Visibility = Visibility.Collapsed;
            isUniqueNo.Item2.Visibility = Visibility.Collapsed;



            if (sessionData.IsPrimary != null)
            {
                if (sessionData.IsPrimary == true)
                {
                    UIHelpers.EnableHighlighting(isPrimaryButtonList, isPrimaryYes.Item1);

                }
                else
                {
                    UIHelpers.EnableHighlighting(isPrimaryButtonList, isPrimaryNo.Item1);
                    isNotNullTitle.Visibility = Visibility.Visible;
                    isNotNullYes.Item2.Visibility = Visibility.Visible;
                    isNotNullNo.Item2.Visibility = Visibility.Visible;
                    isRowUnique.Visibility = Visibility.Visible;
                    isUniqueYes.Item2.Visibility = Visibility.Visible;
                    isUniqueNo.Item2.Visibility = Visibility.Visible;




                    if (sessionData.IsNotNull != null)
                    {
                        if (sessionData.IsNotNull == true)
                        {
                            UIHelpers.EnableHighlighting(isNotNullButtonList, isNotNullYes.Item1);

                        }
                        else
                        {
                            UIHelpers.EnableHighlighting(isNotNullButtonList, isNotNullNo.Item1);

                        }
                    }

                    else
                    {
                        UIHelpers.EnableHighlighting(isNotNullButtonList);

                    }




                    if (sessionData.IsUnique != null)
                    {
                        if (sessionData.IsUnique == true)
                        {
                            UIHelpers.EnableHighlighting(isUniqueButtonList, isUniqueYes.Item1);

                        }
                        else
                        {
                            UIHelpers.EnableHighlighting(isUniqueButtonList, isUniqueNo.Item1);

                        }
                    }

                    else
                    {
                        UIHelpers.EnableHighlighting(isUniqueButtonList);

                    }



                }
            }
            else
            {
                UIHelpers.EnableHighlighting(isPrimaryButtonList);
            }


            // Logic bindings
            isPrimaryYes.Item1.Click += (s, e) =>
            {
                isNotNullTitle.Visibility = Visibility.Collapsed;
                isNotNullYes.Item2.Visibility = Visibility.Collapsed;
                isNotNullNo.Item2.Visibility = Visibility.Collapsed;
                isRowUnique.Visibility = Visibility.Collapsed;
                isUniqueYes.Item2.Visibility = Visibility.Collapsed;
                isUniqueNo.Item2.Visibility = Visibility.Collapsed;
            };

            isPrimaryNo.Item1.Click += (s, e) =>
            {
                isNotNullTitle.Visibility = Visibility.Visible;
                isNotNullYes.Item2.Visibility = Visibility.Visible;
                isNotNullNo.Item2.Visibility = Visibility.Visible;
                isRowUnique.Visibility = Visibility.Visible;
                isUniqueYes.Item2.Visibility = Visibility.Visible;
                isUniqueNo.Item2.Visibility = Visibility.Visible;
            };

            #endregion

            #region Default and Checks Logic

            var defaultAndChecksTab = new TabItem
            {
                Header = "Default & Checks",
            };

            var contentPanelDC = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerDC = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanelDC,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerDC.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerDC.VerticalOffset - e.Delta;
                scrollViewerDC.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };

            defaultAndChecksTab.Content = scrollViewerDC;
            Tabs.Items.Add(defaultAndChecksTab);

            var titleDC = UIHelpers.CreateBasicTitle("Default Value and Check Constraints");
            contentPanelDC.Children.Add(titleDC);

            var hasDefaultTitle = UIHelpers.CreateSubTitle("Is there a default value?");
            contentPanelDC.Children.Add(hasDefaultTitle);

            var hasDefaultYes = UIHelpers.CreateBasicButton("Yes");
            var hasDefaultNo = UIHelpers.CreateBasicButton("No");

            var hasDefaultButtons = new List<Button> { hasDefaultYes.Item1, hasDefaultNo.Item1 }; //I should've done it like this before

            contentPanelDC.Children.Add(hasDefaultYes.Item2);
            contentPanelDC.Children.Add(hasDefaultNo.Item2);

            var defaultValueTitle = UIHelpers.CreateSubTitle("What is the default value?");
            var defaultValueInput = UIHelpers.CreateTextInput("Enter default value (Auto Saved)");
            var isFunctionTitle = UIHelpers.CreateSubTitle("Is the default a Postgres function?");
            var isFunctionYes = UIHelpers.CreateBasicButton("Yes");
            var isFunctionNo = UIHelpers.CreateBasicButton("No");
            var functionButtons = new List<Button> { isFunctionYes.Item1, isFunctionNo.Item1 };

            contentPanelDC.Children.Add(defaultValueTitle);
            defaultValueTitle.Visibility = Visibility.Collapsed;
            contentPanelDC.Children.Add(defaultValueInput.Item2);
            defaultValueInput.Item2.Visibility = Visibility.Collapsed;
            contentPanelDC.Children.Add(isFunctionTitle);
            isFunctionTitle.Visibility = Visibility.Collapsed;
            contentPanelDC.Children.Add(isFunctionYes.Item2);
            isFunctionYes.Item2.Visibility = Visibility.Collapsed;
            contentPanelDC.Children.Add(isFunctionNo.Item2);
            isFunctionNo.Item2.Visibility = Visibility.Collapsed;

            var hasCheckTitle = UIHelpers.CreateSubTitle("Does this value have any checks?");
            contentPanelDC.Children.Add(hasCheckTitle);

            var hasCheckYes = UIHelpers.CreateBasicButton("Yes");
            var hasCheckNo = UIHelpers.CreateBasicButton("No");
            var hasCheckButtons = new List<Button> { hasCheckYes.Item1, hasCheckNo.Item1 };

            contentPanelDC.Children.Add(hasCheckYes.Item2);
            contentPanelDC.Children.Add(hasCheckNo.Item2);

            var valueCheckTitle = UIHelpers.CreateSubTitle("Enter check constraint (Auto Saved):");
            var valueCheckInput = UIHelpers.CreateTextInput("Example: price > 0 ");

            var checkError = UIHelpers.CreateError();
            contentPanelDC.Children.Add(checkError);

            valueCheckInput.Item1.TextChanged += (s, e) =>
            {
                var expr = valueCheckInput.Item1.Text?.Trim() ?? "";
                sessionData.Check = string.IsNullOrWhiteSpace(expr) ? null : expr;

                UIHelpers.ResetError(checkError);

                if (!string.IsNullOrWhiteSpace(expr))
                {
                    if (!Regex.IsMatch(expr, @"^\s*\(?.*\)?\s*$"))
                    {
                        UIHelpers.SetError(checkError,
                            "Check expression looks invalid or unbalanced. Try: (age > 18) AND active = true");
                        return;
                    }
                }
            };

            contentPanelDC.Children.Add(valueCheckTitle);
            valueCheckTitle.Visibility = Visibility.Collapsed;
            contentPanelDC.Children.Add(valueCheckInput.Item2);
            valueCheckInput.Item2.Visibility = Visibility.Collapsed;

            defaultValueInput.Item1.TextChanged += (s, e) =>
            {
                sessionData.DefaultValue = defaultValueInput.Item1.Text;

                if (string.IsNullOrWhiteSpace(defaultValueInput.Item1.Text))
                {
                    sessionData.DefaultValue = null;
                }

            };

            valueCheckInput.Item1.TextChanged += (s, e) =>
            {
                sessionData.Check = valueCheckInput.Item1.Text;

                if (string.IsNullOrWhiteSpace(valueCheckInput.Item1.Text))
                {
                    sessionData.Check = null;
                }
            };



            // Initialize from session data
            if (sessionData.DefaultValue != null)
            {
                defaultValueInput.Item1.Text = sessionData.DefaultValue;
                UIHelpers.EnableHighlighting(hasDefaultButtons, hasDefaultYes.Item1);
                defaultValueTitle.Visibility = Visibility.Visible;
                defaultValueInput.Item2.Visibility = Visibility.Visible;

                if (sessionData.DefaultIsPostgresFunction != null)
                {
                    UIHelpers.EnableHighlighting(functionButtons,
                        sessionData.DefaultIsPostgresFunction.Value ?
                        isFunctionYes.Item1 : isFunctionNo.Item1);
                    isFunctionTitle.Visibility = Visibility.Visible;
                    isFunctionYes.Item2.Visibility = Visibility.Visible;
                    isFunctionNo.Item2.Visibility = Visibility.Visible;
                }
            }
            else
            {
                UIHelpers.EnableHighlighting(hasDefaultButtons, hasDefaultNo.Item1);
            }

            if (sessionData.Check != null)
            {
                valueCheckInput.Item1.Text = sessionData.Check;
                UIHelpers.EnableHighlighting(hasCheckButtons, hasCheckYes.Item1);
                valueCheckTitle.Visibility = Visibility.Visible;
                valueCheckInput.Item2.Visibility = Visibility.Visible;
            }
            else
            {
                UIHelpers.EnableHighlighting(hasCheckButtons, hasCheckNo.Item1);
            }

            // Event Handlers
            hasDefaultYes.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(hasDefaultButtons, hasDefaultYes.Item1);
                defaultValueTitle.Visibility = Visibility.Visible;
                defaultValueInput.Item2.Visibility = Visibility.Visible;
                isFunctionTitle.Visibility = Visibility.Visible;
                isFunctionYes.Item2.Visibility = Visibility.Visible;
                isFunctionNo.Item2.Visibility = Visibility.Visible;

                // Initialize function selection if not set
                if (sessionData.DefaultIsPostgresFunction == null)
                {
                    sessionData.DefaultIsPostgresFunction = false;
                    UIHelpers.EnableHighlighting(functionButtons, isFunctionNo.Item1);
                }
            };

            hasDefaultNo.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(hasDefaultButtons, hasDefaultNo.Item1);
                defaultValueTitle.Visibility = Visibility.Collapsed;
                defaultValueInput.Item2.Visibility = Visibility.Collapsed;
                isFunctionTitle.Visibility = Visibility.Collapsed;
                isFunctionYes.Item2.Visibility = Visibility.Collapsed;
                isFunctionNo.Item2.Visibility = Visibility.Collapsed;
                defaultValueInput.Item1.Text = "";
                sessionData.DefaultValue = null;
                sessionData.DefaultIsPostgresFunction = null;
            };

            hasCheckYes.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(hasCheckButtons, hasCheckYes.Item1);
                valueCheckTitle.Visibility = Visibility.Visible;
                valueCheckInput.Item2.Visibility = Visibility.Visible;
            };

            hasCheckNo.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(hasCheckButtons, hasCheckNo.Item1);
                valueCheckTitle.Visibility = Visibility.Collapsed;
                valueCheckInput.Item2.Visibility = Visibility.Collapsed;
                valueCheckInput.Item1.Text = "";
                sessionData.Check = null;
            };

            // Function selection handlers
            isFunctionYes.Item1.Click += (s, e) =>
            {
                sessionData.DefaultIsPostgresFunction = true;
                UIHelpers.EnableHighlighting(functionButtons, isFunctionYes.Item1);
            };

            isFunctionNo.Item1.Click += (s, e) =>
            {
                sessionData.DefaultIsPostgresFunction = false;
                UIHelpers.EnableHighlighting(functionButtons, isFunctionNo.Item1);
            };



            #endregion

            #region Finalize

            var finalizeTab = new TabItem
            {
                Header = "Finalize",
            };

            var contentPanelFinal = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerFinal = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanelFinal,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerFinal.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerFinal.VerticalOffset - e.Delta;
                scrollViewerFinal.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };

            finalizeTab.Content = scrollViewerFinal;
            Tabs.Items.Add(finalizeTab);



            Tabs.SelectionChanged += (sender, args) =>
            {
                contentPanelFinal.Children.Clear();

                var titleFinalized = UIHelpers.CreateBasicTitle("Are You Finished?");
                contentPanelFinal.Children.Add(titleFinalized);


                // RowType and Limit
                string limitText = sessionData.Limit != null
                    ? $"with a limit of {sessionData.Limit}"
                    : "with no limit";

                string rowTypeText = sessionData.RowType?.ToString() ?? "Unknown type";

                // IsArray and ArrayLimit
                string arrayText;
                if (sessionData.IsArray == true)
                {
                    arrayText = sessionData.ArrayLimit != null
                        ? $"it's an array with a size limit of {sessionData.ArrayLimit}"
                        : "it's an array with no size limit";
                }
                else if (sessionData.IsArray == false)
                {
                    arrayText = "it's not an array";
                }
                else
                {
                    arrayText = "array status is unknown";
                }



                contentPanelFinal.Children.Add(UIHelpers.CreateParagraph(
                    $"This is a ({rowTypeText}) {limitText}; {arrayText}."));

                // Primary, Unique, NotNull
                string primaryText = sessionData.IsPrimary == true ? "" : "NOT ";
                string uniqueText = sessionData.IsUnique == true ? "" : "not ";
                string notNullText = sessionData.IsNotNull == true
                    ? "is required (NOT NULL)"
                    : sessionData.IsNotNull == false
                        ? "is optional (nullable)"
                        : "nullability unknown";

                contentPanelFinal.Children.Add(UIHelpers.CreateParagraph(
                    $"This row is {primaryText}a primary key, {uniqueText}unique, and {notNullText}."));

                // Default
                if (!string.IsNullOrWhiteSpace(sessionData.DefaultValue))
                {
                    var defaultLine = $"Default value is: {sessionData.DefaultValue}";
                    if (sessionData.DefaultIsPostgresFunction == true)
                        defaultLine += " (PostgreSQL function)";

                    contentPanelFinal.Children.Add(UIHelpers.CreateSubTitle("What is the default value?"));
                    contentPanelFinal.Children.Add(UIHelpers.CreateParagraph(defaultLine));
                }

                // Check constraint
                if (!string.IsNullOrWhiteSpace(sessionData.Check))
                {
                    contentPanelFinal.Children.Add(UIHelpers.CreateSubTitle("Check Constraint"));
                    contentPanelFinal.Children.Add(UIHelpers.CreateParagraph(sessionData.Check));
                }

                bool IsValidRowOptions(RowCreation opts)
                {
                    if (string.IsNullOrWhiteSpace(opts.Name) ||
                        string.IsNullOrWhiteSpace(opts.Description))
                    {
                        return false;
                    }
                    var fieldName = NameOfRow.Item1.Text;


                    bool nameExists = refTable.Rows.Any(r =>
                            string.Equals(r.Name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                            r.GetHashCode() != sessionDataHistory.GetHashCode()
                        );

                    if (nameExists)
                    {
                        return false;
                    }

                    bool hasTypeDefiner =
                           opts.RowType != null
                        || opts.EncryptedAndNOTMedia != null
                        || (opts.Media == true);

                    return hasTypeDefiner;
                }






                if(IsValidRowOptions(sessionData))
                {
                    var finalizeText = UIHelpers.CreateSubTitle("You can finalize this row!");
                    var finalizeButton = UIHelpers.CreateBasicButton("Finalize");

                    contentPanelFinal.Children.Add(finalizeText);
                    contentPanelFinal.Children.Add(finalizeButton.Item2);


                    finalizeButton.Item1.Click += (s, e) =>
                    {
                        Step.Visibility = Visibility.Collapsed;
                        Finalized.Visibility = Visibility.Visible;


                        // SessionInfo.Rows.Add(sessionData);
                        int refTableHash = sessionDataHistory.GetHashCode();

                        var SessionTable = mainPage.MainSessionInfo.Tables.SingleOrDefault(s => s.GetHashCode() == refTable.GetHashCode());



                        int index;

                        if (originalIsNull || SessionTable.Rows == null)
                        {
                            index = -1;
                        }
                        else
                        {
                            index = SessionTable.Rows.IndexOf((RowCreation)sessionDataHistory);
                        }


                        // Add or replace
                        if (index == -1)
                        {
                            SessionTable.Rows.Add(sessionData);

                        }
                        else
                        {

                            SessionTable.Rows.RemoveAt(index);
                            SessionTable.Rows.Insert(index, sessionData);



                            //Find all referenes and replace
                            //sessionData
                            foreach (var item in mainPage.MainSessionInfo.Tables)

                            {
                                if (item.References != null)
                                {
                                    for (int i = 0; i < item.References.Count; i++)
                                    {
                                        var refitem = item.References[i]; // make a copy

                                        if (refitem.MainTable == $"{SessionTable.SchemaName}.{SessionTable.TableName}" &&
                                            refitem.ForeignKey == sessionDataHistory.Name)
                                        {
                                            refitem.ForeignKey = sessionData.Name;
                                        }

                                        if (refitem.RefTable == $"{SessionTable.SchemaName}.{SessionTable.TableName}" &&
                                            refitem.RefTableKey == sessionDataHistory.Name)
                                        {
                                            refitem.RefTableKey = sessionData.Name;
                                        }

                                        item.References[i] = refitem; // write the modified struct back
                                    }

                                }

                            }

                        }

                        mainPage.ForceCollectionChangeUpate();
                        DBViewer.PopulatePage();


                        if (DBViewer != null)
                        {
                            DBViewer.selectedItem = sessionData;
                            DBViewer.LoadItemView(sessionData);
                        }



                    };


                }

                else
                {
                    var finalizeText = UIHelpers.CreateSubTitle("Row can't be finalized yet! Title, Description and Type Required!");

                    contentPanelFinal.Children.Add(finalizeText);


                }

                ClosePage.Click += (s, e) =>
                {
RemoveWindow();
                };


            };

            #endregion




        }

        public void CreateReference(SessionStorage.TableObject refTable, SessionStorage.ReferenceOptions? referenceItem)
        {
            //Three Sections; Reference Basics, On Update, On Delete

            Tabs.Items.Clear();

            if (referenceItem == null)
            {
                referenceItem = new SessionStorage.ReferenceOptions();
            }

            var referenceData = (SessionStorage.ReferenceOptions)referenceItem;

            var savedRefItem = referenceData; //We use this for update logic later

            var originalIsNull = referenceItem == null;

            if (refTable.References == null)
            {
                refTable.References = new List<SessionStorage.ReferenceOptions>();
            }

            //Creates the basic tabs area

            #region Basic Setup

            var BasicsTab = new TabItem
            {
                Header = "Basics",
            };

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerForLimits = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanel,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerForLimits.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerForLimits.VerticalOffset - e.Delta;
                scrollViewerForLimits.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };


            BasicsTab.Content = scrollViewerForLimits;
            Tabs.Items.Add(BasicsTab);


            SessionStorage.RowCreation? rowReferencedInTableA;

            SessionStorage.TableObject? tableB;

            BasicsTab.Content = contentPanel;

            var Title = UIHelpers.CreateBasicTitle("What rows are going to be connected?");

            var SubtitleA = UIHelpers.CreateSubTitle("What Row Will Be Affected On This Table?");
            var SubtitleB = UIHelpers.CreateSubTitle("Note: The referenced columns MUST be unique or a primary key, else it won't work!");

            var rowsOnThisTable = refTable.Rows.Select(s => s.Name).ToList();

            var dropdownA = UIHelpers.CreateDropdown(rowsOnThisTable, $"Rows on {refTable.TableName}");

            if (referenceData.ForeignKey != null)
            {
                foreach (ComboBoxItem item in dropdownA.Item1.Items)
                {

                    if (item.Content == referenceData.ForeignKey)
                    {
                        dropdownA.Item1.SelectedItem = item;
                    }

                }
            }



            var SubtitleC = UIHelpers.CreateSubTitle("What Table Will We Be Changing Based On The First?");
            var ParagraphA = UIHelpers.CreateParagraph("The referenced row must match one from the first table. For example, pick a name from Table A (like Jeff or Mary) to look for here.");

            contentPanel.Children.Add(ParagraphA);

  


            var TableOptions = mainPage.MainSessionInfo.Tables
    .Select(t => string.IsNullOrEmpty(t.SchemaName)
                    ? t.TableName
                    : t.SchemaName + "." + t.TableName)
    .ToList();

            var dropdownB = UIHelpers.CreateDropdown(TableOptions, "Tables");

            contentPanel.Children.Add(Title);
            contentPanel.Children.Add(SubtitleA);
            contentPanel.Children.Add(SubtitleB);
            contentPanel.Children.Add(dropdownA.Item2);
            contentPanel.Children.Add(SubtitleC);
            contentPanel.Children.Add(dropdownB.Item2);

            dropdownA.Item1.SelectionChanged += (s, e) =>
            {
                var selectedRow = refTable.Rows.SingleOrDefault(s => s.Name == dropdownA.Item1.SelectedItem.ToString());

                rowReferencedInTableA = selectedRow;
            };

            if (referenceData.RefTable != null)
            {
                for (int i = 0; i < dropdownB.Item1.Items.Count; i++)
                {
                    var cbi = (ComboBoxItem)dropdownB.Item1.Items[i];
                    if (cbi.Content?.ToString() == referenceData.RefTable)
                    {
                        dropdownB.Item1.SelectedIndex = i;
                        break;
                    }
                }
            }





            bool DropdownSetup = false;


            if (referenceData.RefTable != null && referenceData.RefTableKey != null)
            {
                SetupDropdownC();
            }



            dropdownB.Item1.SelectionChanged += (s, e) =>
            {

                SetupDropdownC();

            };

            void SetupDropdownC()
            {

                var cbi = (ComboBoxItem)dropdownB.Item1.SelectedItem;
                var selectedName = (string)cbi.Content;
                var selectedTable = mainPage.MainSessionInfo.Tables
                    .Single(t =>
                        string.IsNullOrEmpty(t.SchemaName)
                            ? t.TableName == selectedName
                            : t.SchemaName + "." + t.TableName == selectedName
                    );


                tableB = selectedTable;

                ParagraphA.Visibility = Visibility.Visible;

                List<string> RowOptions = selectedTable.Rows.Select(s => s.Name).ToList();

                if (DropdownSetup == false)
                {
                    DropdownSetup = true;
                    var dropdownSetup = UIHelpers.CreateDropdown(RowOptions, "Rows on Selected Table");
                    contentPanel.Children.Add(dropdownSetup.Item2);

                    dropdownSetup.Item1.SelectionChanged += (s, e) =>
                    {

                        var cbi = (ComboBoxItem)dropdownSetup.Item1.SelectedItem;

                        var selectedName = (string)cbi.Content;

                        referenceData.RefTableKey = selectedName;


                    };


                    if (referenceData.RefTable != null && referenceData.RefTableKey != null)
                    {
                        foreach (ComboBoxItem item in dropdownSetup.Item1.Items)
                        {

                            if (item.Content == referenceData.RefTableKey)
                            {
                                dropdownSetup.Item1.SelectedItem = item;
                            }

                        }
                    }

                }

                else
                {
                    UIHelpers.TrimStackPanel(contentPanel, contentPanel.Children.Count - 1);
                    var dropdownSetup = UIHelpers.CreateDropdown(RowOptions, "Rows on Selected Table");
                    contentPanel.Children.Add(dropdownSetup.Item2);

                    dropdownSetup.Item1.SelectionChanged += (s, e) =>
                    {

                        var cbi = (ComboBoxItem)dropdownSetup.Item1.SelectedItem;

                        var selectedName = (string)cbi.Content;

                        referenceData.RefTableKey = selectedName;


                    };

                    if (referenceData.RefTable != null && referenceData.RefTableKey != null)
                    {
                        foreach (ComboBoxItem item in dropdownSetup.Item1.Items)
                        {

                            if (item.Content == referenceData.RefTableKey)
                            {
                                dropdownSetup.Item1.SelectedItem = item;
                            }

                        }
                    }



                }
            }


            referenceData.MainTable = refTable.TableName;


            dropdownA.Item1.SelectionChanged += (s, e) =>
            {

                var cbi = (ComboBoxItem)dropdownA.Item1.SelectedItem;

                var selectedName = (string)cbi.Content;

                referenceData.ForeignKey = selectedName;


            };

            dropdownB.Item1.SelectionChanged += (s, e) =>
            {

                var cbi = (ComboBoxItem)dropdownB.Item1.SelectedItem;

                var selectedName = (string)cbi.Content;

                referenceData.RefTable = selectedName;


            };

            


            //Initial Setup










            #endregion


            #region On Update


            var OnUpdateTab = new TabItem
            {
                Header = "On Update",
            };

            var contentPanel2 = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };


            Tabs.Items.Add(OnUpdateTab);

            OnUpdateTab.Content = contentPanel2;

            // UI Elements
            var TitleOnUpdate = UIHelpers.CreateBasicTitle("What will happen when this updates?");

            var scalarLimitTitle = UIHelpers.CreateSubTitle("Do variables in this row have a length limit?");


            var OnUpdate1 = UIHelpers.CreateBasicButton("When the referenced key is updated, all related keys in this table are updated as well.");
            var OnUpdate2 = UIHelpers.CreateBasicButton("When the referenced key is updated, related foreign key columns in this table are set to NULL.");
            var OnUpdate3 = UIHelpers.CreateBasicButton("When the referenced key is updated, related foreign key columns in this table are set to their default values.");
            var OnUpdate4 = UIHelpers.CreateBasicButton("Updating the referenced key is blocked if it's still being used in this table.");
            var OnUpdate5 = UIHelpers.CreateBasicButton("No automatic changes happen when the referenced key is updated.");



            // Add controls to the scrollable panel
            contentPanel2.Children.Add(TitleOnUpdate);

            contentPanel2.Children.Add(scalarLimitTitle);

            contentPanel2.Children.Add(OnUpdate1.Item2);
            contentPanel2.Children.Add(OnUpdate2.Item2);
            contentPanel2.Children.Add(OnUpdate3.Item2);
            contentPanel2.Children.Add(OnUpdate4.Item2);
            contentPanel2.Children.Add(OnUpdate5.Item2);

            var OnUpdateList = new List<Button> { OnUpdate1.Item1, OnUpdate2.Item1, OnUpdate3.Item1, OnUpdate4.Item1, OnUpdate5.Item1 };

            Button? Highlighted = null;

            switch (referenceData.OnUpdateAction)
            {
                case ReferentialAction.Cascade:
                    Highlighted = OnUpdate1.Item1;
                    break;
                case ReferentialAction.SetNull:
                    Highlighted = OnUpdate2.Item1;
                    break;
                case ReferentialAction.SetDefault:
                    Highlighted = OnUpdate3.Item1;
                    break;
                case ReferentialAction.Restrict:
                    Highlighted = OnUpdate4.Item1;
                    break;
                case ReferentialAction.NoAction:
                    Highlighted = OnUpdate5.Item1;
                    break;
                default:
                    Highlighted = OnUpdate5.Item1;
                    break;
            }

            UIHelpers.EnableHighlighting(OnUpdateList, Highlighted);

            if (referenceData.OnUpdateAction == default )
            {
                referenceData.OnUpdateAction = ReferentialAction.NoAction;
            }


            OnUpdate1.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnUpdateList, OnUpdate5.Item1);
                referenceData.OnUpdateAction = ReferentialAction.Cascade;

            };

            OnUpdate2.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnUpdateList, OnUpdate2.Item1);
                referenceData.OnUpdateAction = ReferentialAction.SetNull;
            };

            OnUpdate3.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnUpdateList, OnUpdate3.Item1);
                referenceData.OnUpdateAction = ReferentialAction.SetDefault;
            };

            OnUpdate4.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnUpdateList, OnUpdate4.Item1);
                referenceData.OnUpdateAction = ReferentialAction.Restrict;
            };

            OnUpdate5.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnUpdateList, OnUpdate5.Item1);
                referenceData.OnUpdateAction = ReferentialAction.NoAction;
            };




            #endregion


            #region On Delete

            var OnDeleteTab = new TabItem
            {
                Header = "On Delete",
            };

            var contentPanel3 = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };


            Tabs.Items.Add(OnDeleteTab);

            OnDeleteTab.Content = contentPanel3;

            // UI Elements
            var TitleOnDelete = UIHelpers.CreateBasicTitle("What will happen when this Deletes?");

            var onDeleteSub = UIHelpers.CreateSubTitle("Do variables in this row have a length limit?");


            var OnDelete1 = UIHelpers.CreateBasicButton("When a referenced row is deleted, all related rows in this table are also deleted.");
            var OnDelete2 = UIHelpers.CreateBasicButton("When a referenced row is deleted, related foreign key columns in this table are set to NULL.");
            var OnDelete3 = UIHelpers.CreateBasicButton("When a referenced row is deleted, related foreign key columns in this table are set to their default values.");
            var OnDelete4 = UIHelpers.CreateBasicButton("Deletion of the referenced row is blocked if it's still being used in this table.");
            var OnDelete5 = UIHelpers.CreateBasicButton("No automatic changes happen when a referenced row is deleted.");



            // Add controls to the scrollable panel
            contentPanel3.Children.Add(TitleOnDelete);

            contentPanel3.Children.Add(onDeleteSub);

            contentPanel3.Children.Add(OnDelete1.Item2);
            contentPanel3.Children.Add(OnDelete2.Item2);
            contentPanel3.Children.Add(OnDelete3.Item2);
            contentPanel3.Children.Add(OnDelete4.Item2);
            contentPanel3.Children.Add(OnDelete5.Item2);


            var OnDeleteList = new List<Button> { OnDelete1.Item1, OnDelete2.Item1, OnDelete3.Item1, OnDelete4.Item1, OnDelete5.Item1 };


            Button? Highlighted2 = null;

            switch (referenceData.OnDeleteAction)
            {
                case ReferentialAction.Cascade:
                    Highlighted2 = OnDelete1.Item1;
                    break;
                case ReferentialAction.SetNull:
                    Highlighted2 = OnDelete2.Item1;
                    break;
                case ReferentialAction.SetDefault:
                    Highlighted2 = OnDelete3.Item1;
                    break;
                case ReferentialAction.Restrict:
                    Highlighted2 = OnDelete4.Item1;
                    break;
                case ReferentialAction.NoAction:
                    Highlighted2 = OnDelete5.Item1;
                    break;
                default:
                    Highlighted = OnDelete5.Item1;
                    break;
            }

            UIHelpers.EnableHighlighting(OnUpdateList, Highlighted);

            if (referenceData.OnDeleteAction == default)
            {
                referenceData.OnDeleteAction = ReferentialAction.NoAction;
            }

            UIHelpers.EnableHighlighting(OnDeleteList, Highlighted2);

            OnDelete1.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnDeleteList, OnDelete1.Item1);
            };

            OnDelete1.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnDeleteList, OnDelete1.Item1);
                referenceData.OnDeleteAction = ReferentialAction.Cascade;
            };

            OnDelete2.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnDeleteList, OnDelete2.Item1);
                referenceData.OnDeleteAction = ReferentialAction.SetNull;
            };

            OnDelete3.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnDeleteList, OnDelete3.Item1);
                referenceData.OnDeleteAction = ReferentialAction.SetDefault;
            };

            OnDelete4.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnDeleteList, OnDelete4.Item1);
                referenceData.OnDeleteAction = ReferentialAction.Restrict;
            };

            OnDelete5.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(OnDeleteList, OnDelete5.Item1);
                referenceData.OnDeleteAction = ReferentialAction.NoAction;
            };


            #endregion


            #region Finalize

            var finalizeTab = new TabItem
            {
                Header = "Finalize",
            };

            var contentPanelFinal = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerFinal = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanelFinal,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerFinal.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerFinal.VerticalOffset - e.Delta;
                scrollViewerFinal.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };

            finalizeTab.Content = scrollViewerFinal;
            Tabs.Items.Add(finalizeTab);

            Tabs.SelectionChanged += (sender, args) =>
            {
                contentPanelFinal.Children.Clear();

                var titleFinalized = UIHelpers.CreateBasicTitle("Are You Finished?");
                contentPanelFinal.Children.Add(titleFinalized);


                // ON UPDATE description via switch-statement
                string updateActionDescription = default;
                switch (referenceData.OnUpdateAction)
                {
                    case ReferentialAction.Cascade:
                        updateActionDescription = "When the referenced key is updated, all related keys in this table are updated as well.";
                        break;
                    case ReferentialAction.SetNull:
                        updateActionDescription = "When the referenced key is updated, related foreign key columns in this table are set to NULL.";
                        break;
                    case ReferentialAction.SetDefault:
                        updateActionDescription = "When the referenced key is updated, related foreign key columns in this table are set to their default values.";
                        break;
                    case ReferentialAction.Restrict:
                        updateActionDescription = "Updating the referenced key is blocked if it's still being used in this table.";
                        break;
                    default:
                        updateActionDescription = "Nothing Happens.";
                        break;
                }

                // ON DELETE description via switch-statement
                string deleteActionDescription = default;
                switch (referenceData.OnDeleteAction)
                {
                    case ReferentialAction.Cascade:
                        deleteActionDescription = "When a referenced row is deleted, all related rows in this table are also deleted.";
                        break;
                    case ReferentialAction.SetNull:
                        deleteActionDescription = "When a referenced row is deleted, related foreign key columns in this table are set to NULL.";
                        break;
                    case ReferentialAction.SetDefault:
                        deleteActionDescription = "When a referenced row is deleted, related foreign key columns in this table are set to their default values.";
                        break;
                    case ReferentialAction.Restrict:
                        deleteActionDescription = "Deletion of the referenced row is blocked if it's still being used in this table.";
                        break;
                    default:
                        deleteActionDescription = "Nothing Happens.";
                        break;
                }


                contentPanelFinal.Children.Add(UIHelpers.CreateSubTitle("Foreign Key Behavior"));



                contentPanelFinal.Children.Add(UIHelpers.CreateParagraph($"ON UPDATE: {updateActionDescription}"));
                contentPanelFinal.Children.Add(UIHelpers.CreateParagraph($"ON DELETE: {deleteActionDescription}"));



                if (referenceData.MainTable != null && referenceData.RefTable != null && referenceData.ForeignKey != null && referenceData.RefTableKey != null)
                {
                    var finalizeText = UIHelpers.CreateSubTitle("You can finalize this reference!");
                    var finalizeButton = UIHelpers.CreateBasicButton("Finalize");

                    contentPanelFinal.Children.Add(finalizeText);
                    contentPanelFinal.Children.Add(finalizeButton.Item2);

                    finalizeButton.Item1.Click += (s, e) =>
                    {
                        Step.Visibility = Visibility.Collapsed;
                        Finalized.Visibility = Visibility.Visible;

                        // SessionInfo.Rows.Add(sessionData);

                        var SessionTable = mainPage.MainSessionInfo.Tables.SingleOrDefault(s => s.GetHashCode() == refTable.GetHashCode());


                        int index;

                        if (originalIsNull || SessionTable.References == null)
                        {
                            index = -1;
                        }
                        else
                        {
                            index = SessionTable.References.IndexOf((SessionStorage.ReferenceOptions)savedRefItem);
                        }


                        // Add or replace
                        if (index == -1)
                        {
                            SessionTable.References.Add(referenceData);

                        }
                        else
                        {

                            SessionTable.References.RemoveAt(index);
                            SessionTable.References.Insert(index, referenceData);

                        }



                        if (DBViewer != null)
                        {
                            DBViewer.LoadItemView(savedRefItem);
                        }

                        mainPage.ForceCollectionChangeUpate();
                        DBViewer.PopulatePage();

                    };


                }



                else
                {
                    var finalizeText = UIHelpers.CreateSubTitle("Reference can't be finalized yet! Origin and Referenced Fields Required!");

                    contentPanelFinal.Children.Add(finalizeText);


                }




            };





            #endregion



        }





        public List<TableObject>? DefaultProjectData = null;
        //Where we dump the new project

        public ObservableCollection<DBDesignerSession> TemplatedItem = new ObservableCollection<DBDesignerSession>(); 
        public string TemplateName;
        public string AuthorName;

        public List<TableObject>? CustomTables = null;
        //Custom Tables from Template

        public Dictionary<string, List<TableObject>> TemplateProjectRows = new Dictionary<string, List<TableObject>>();
        public ObservableCollection<string> TemplateNames = new ObservableCollection<string>();
        public ObservableCollection<string> TemplatePaths = new ObservableCollection<string>();


        public List<MainDesigner> Designers = new List<MainDesigner>();





        public void CreateProject()
        {

            Tabs.Items.Clear();

            var BasicsTab = new TabItem
            {
                Header = "Basics",
            };

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var scrollViewerForLimits = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentPanel,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };

            scrollViewerForLimits.MouseWheel += (sender, e) =>
            {
                double newOffset = scrollViewerForLimits.VerticalOffset - e.Delta;
                scrollViewerForLimits.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };


            BasicsTab.Content = scrollViewerForLimits;
            Tabs.Items.Add(BasicsTab);




            string ProjectTitle = default;
            string DescriptionName = default;
            //Project Name and Description

            #region UI Setting

            var Title1 = UIHelpers.CreateBasicTitle("Create New Project");
            var Subtitle1 = UIHelpers.CreateSubTitle("1. Do you want to create a new project or edit a template?");

            var CreateNewButton = UIHelpers.CreateBasicButton("Create New Project"); //
            var EditTemplateButton = UIHelpers.CreateBasicButton("Edit Template"); //

            var Text1 = UIHelpers.CreateParagraph($"ProjectData: {0}");

            var Subtitle2 = UIHelpers.CreateSubTitle("2. Would you like to use template tables or start with the defaults?");

            var DefaultsButton = UIHelpers.CreateBasicButton("Use Default Tables"); //
            var UseTemplateButton = UIHelpers.CreateBasicButton("Use Template Tables"); //

            var Text2 = UIHelpers.CreateParagraph($"ProjectData: {0}");
            Text2.TextWrapping = TextWrapping.Wrap;

            var Subtitle3 = UIHelpers.CreateSubTitle("3. What is the name of your project?");

            var ProjectNameText = UIHelpers.CreateTextInput("Project Name");
            ProjectNameText.Item1.MaxLength = 64;
            ProjectNameText.Item1.Text = "";

            var Subtitle4 = UIHelpers.CreateSubTitle("4. What is the description of your project?");

            var ProjectDescriptionText = UIHelpers.CreateTextInput("Project Description");
            ProjectDescriptionText.Item1.MaxLength = 1024;
            ProjectDescriptionText.Item1.Text = "";


            var ErrorText = UIHelpers.CreateError();


            var ImgName = UIHelpers.CreateTextInput("Project Image");

            var Subtitle5 = UIHelpers.CreateSubTitle("5. Select An Image For Your Project (Should Be A Square)");


           var logoButton = UIHelpers.CreateBasicButton("Select Image (Coming Soon, I Got Lazy ;) ");


            var FinalizeButton = UIHelpers.CreateBasicButton("Finalize Project");

            #endregion

            #region Children Add


            contentPanel.Children.Add(Title1);
            contentPanel.Children.Add(Subtitle1);
            contentPanel.Children.Add(CreateNewButton.Item2);
            contentPanel.Children.Add(EditTemplateButton.Item2);
            contentPanel.Children.Add(Text1);
            contentPanel.Children.Add(Subtitle2);
            contentPanel.Children.Add(DefaultsButton.Item2);
            contentPanel.Children.Add(UseTemplateButton.Item2);
            contentPanel.Children.Add(Text2);
            contentPanel.Children.Add(Subtitle3);
            contentPanel.Children.Add(ProjectNameText.Item2);
            contentPanel.Children.Add(Subtitle4);
            contentPanel.Children.Add(ProjectDescriptionText.Item2);
            contentPanel.Children.Add(ErrorText);
       //     contentPanel.Children.Add(ImgName.Item2);
         //   contentPanel.Children.Add(Subtitle5);
         //   contentPanel.Children.Add(logoButton.Item2);

            // Create the Image control
            Image myImage = new Image
            {
                Width = 500,
                Height = 500,
                Stretch = Stretch.UniformToFill // Auto crop to fill
            };

            myImage.Visibility = Visibility.Collapsed;


            contentPanel.Children.Add(FinalizeButton.Item2);

            void UpdateFinalizeButtonState()
            {
                bool hasName = !string.IsNullOrWhiteSpace(ProjectNameText.Item1.Text);
                bool hasDescription = !string.IsNullOrWhiteSpace(ProjectDescriptionText.Item1.Text);

                if (hasName && hasDescription)
                {
                    FinalizeButton.Item1.IsEnabled = true;
                    UIHelpers.ResetError(ErrorText);
                }
                else
                {
                    FinalizeButton.Item1.IsEnabled = false;
                    UIHelpers.SetError(ErrorText, "Please enter a project name and description.");
                }
            }

            UpdateFinalizeButtonState();



            #endregion

            var Templates = new List<Button> { CreateNewButton.Item1, EditTemplateButton.Item1 };
            UIHelpers.EnableHighlighting(Templates, CreateNewButton.Item1);


            var TableOptions = new List<Button> { UseTemplateButton.Item1, DefaultsButton.Item1 };
            UIHelpers.EnableHighlighting(TableOptions, DefaultsButton.Item1);


            CreateNewButton.Item1.Click += (s, e) =>
            {
                TemplatedItem.Clear();
                TemplateName = default;
                AuthorName = default;
                UIHelpers.EnableHighlighting(Templates, CreateNewButton.Item1);

            };

            EditTemplateButton.Item1.Click += (s, e) =>
            {
                DatabaseTemplates DBT = default;
                mainPage.CreateWindows("Template", () => DBT = new DatabaseTemplates(mainPage, this, mainPage.SeshDirectory.ConvertToString()), false);
                UIHelpers.EnableHighlighting(Templates, EditTemplateButton.Item1);
                //Load options
            };

            UseTemplateButton.Item1.Click += (s, e) =>
            {
                MainDesigner MD = default;
                mainPage.CreateWindows("Template", () => MD = new MainDesigner(this, mainPage, mainPage.SeshDirectory.ConvertToString() + "/" + mainPage.SeshUsername.ConvertToString()), false);
                UIHelpers.EnableHighlighting(TableOptions, UseTemplateButton.Item1);
                //Load options
                foreach (var screen in Designers)
                {
                    mainPage.DesktopCanvas.Children.Remove(screen);
                }
            };

            DefaultsButton.Item1.Click += (s, e) =>
            {
                //Custom tables unused 
                CustomTables = new List<TableObject>();
                UIHelpers.EnableHighlighting(TableOptions, DefaultsButton.Item1);

                TemplateProjectRows.Clear();
                TemplateNames.Clear();
                TemplatePaths.Clear();


                SetText1();

                foreach (var screen in Designers)
                {
                    mainPage.DesktopCanvas.Children.Remove(screen);
                }

            };



            void SetText1()
            {
                if (TemplatedItem.Count >= 1)
                {
                    Text1.Text = $"Project Selected: {TemplateName} by {AuthorName}.";
                }

                else
                {
                    Text1.Text = "No Template Selected.";
                }
                
            }

            void SetText2()
            {
                if (TemplateNames.Count == 0)
                {
                    Text2.Text = $"No Template Selected";
                }
                else
                {

                    var str = new StringBuilder();

                    str.AppendLine($"{TemplateNames.Count} Templates Selected.");

                    foreach (var item in TemplateNames)
                    {
                        str.AppendLine($"Template Pack: {item}");
                    }



                    Text2.Text = str.ToString();
                }
            }


            SetText1();
            SetText2();

            TemplatePaths.CollectionChanged += (s, e) =>
            {
                SetText2();
            };

            TemplatedItem.CollectionChanged += (s, e) =>
            {
                ProjectTitle = ProjectNameText.Item1.Text;
                SetText1();
            };



            ProjectNameText.Item1.TextChanged += (s, e) =>
            {
                ProjectTitle = ProjectNameText.Item1.Text;

                var pn1 = ProjectNameText.Item1.Text;
                var pn2 = ProjectDescriptionText.Item1.Text;

                if (pn1 == null  || pn2 == null)
                {
                    FinalizeButton.Item1.IsEnabled = false;
                    UIHelpers.SetError(ErrorText, "Please Enter A Project Name and Description!");
                }

                else
                {
                    FinalizeButton.Item1.IsEnabled = true;
                }

            };

            ProjectDescriptionText.Item1.TextChanged += (s, e) =>
            {
                DescriptionName = ProjectDescriptionText.Item1.Text;

                var pn1 = ProjectNameText.Item1.Text;
                var pn2 = ProjectDescriptionText.Item1.Text;

                if (pn1 == null || pn2 == null)
                {
                    FinalizeButton.Item1.IsEnabled = false;
                    UIHelpers.SetError(ErrorText, "Please Enter A Project Name and Description!");
                }

                else
                {
                    FinalizeButton.Item1.IsEnabled = true;
                }
            };







            FinalizeButton.Item1.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(ProjectTitle) || string.IsNullOrWhiteSpace(DescriptionName))
                {
                    UIHelpers.SetError(ErrorText, "Please enter both a project name and description.");
                    return;
                }

                // Optional: Disable button and show loading state
                FinalizeButton.Item1.IsEnabled = false;
                FinalizeButton.Item1.Content = "Creating Project...";

                try
                {
                    var CurrentDirectory = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "Projects");
                    string fileName = ProjectTitle;
                    string extension = "secdbdesign";

                    // Check if project already exists
                    string existsUrl = $"{baseUrl}File/Exists?directory={Uri.EscapeDataString(CurrentDirectory)}&fileName={Uri.EscapeDataString(fileName)}&extension={extension}";
                    var existsResponse = await mainPage.DBDesignerClient.GetAsync(existsUrl);
                    var existsResultJson = await existsResponse.Content.ReadAsStringAsync();

                    using var doc = JsonDocument.Parse(existsResultJson);
                    bool exists = doc.RootElement.GetProperty("exists").GetBoolean();

                    if (exists)
                    {
                        UIHelpers.SetError(ErrorText, "A project with this name already exists!");
                        return;
                    }

                    // Build the session data
                    var finalizedTables = new ObservableCollection<TableObject>();

                    if (TemplatedItem?.Count > 0)
                    {
                        foreach (var item in TemplatedItem[0].Tables)
                        {
                            finalizedTables.Add(item);
                        }
                    }

                    if (TemplateProjectRows != null)
                    {
                        foreach (var item in TemplateProjectRows.Values)
                        {
                            foreach (var tableobj in item)
                            {
                                finalizedTables.Add(tableobj);
                            }
                        }
                    }

                    mainPage.MainSessionInfo = new SessionStorage.DBDesignerSession()
                    {
                        SessionName = ProjectTitle,
                        SessionDescription = DescriptionName,
                        LastEdited = DateTime.Now,
                        SessionLogo = default,
                        Tables = finalizedTables,
                        WindowStatuses = new Dictionary<string, SessionStorage.Coords>()
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    string json = JsonSerializer.Serialize(mainPage.MainSessionInfo, options);
                    var securedJson = json.ToSecureData();
                    var contentBase64 = Convert.ToBase64String(securedJson.ConvertToBytes());

                    var SaveDirectory = Path.Combine(
                        mainPage.SeshDirectory.ConvertToString(),
                        mainPage.SeshUsername.ConvertToString(),
                        "Projects",
                        ProjectTitle
                    );

                    var payload = new
                    {
                        Directory = SaveDirectory,
                        FileName = ProjectTitle,
                        Extension = "secdbdesign",
                        Content = contentBase64
                    };

                    var json2 = JsonSerializer.Serialize(payload);
                    var httpContent = new StringContent(json2, Encoding.UTF8, "application/json");

                    var createUrl = $"{baseUrl}File/Create";
                    var createdResponse = await mainPage.DBDesignerClient.PostAsync(createUrl, httpContent);

                    if (!createdResponse.IsSuccessStatusCode)
                    {
                        UIHelpers.SetError(ErrorText, "Failed to save project. Check server connection or permissions.");
                        return;
                    }

                    // Success!
                    mainPage.ProjectName = ProjectTitle;
                    mainPage.ForceCollectionChangeUpate();

                    Step.Visibility = Visibility.Collapsed;
                    Finalized.Visibility = Visibility.Visible;

                    // Close all other open windows
                    var desktopWindows = mainPage.IntroPage.Children.ToList();
                    foreach (var item in desktopWindows)
                    {
                        if (item != this && mainPage.IntroPage.Children.Contains(item))
                        {
                            mainPage.IntroPage.Children.Remove(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Optional: log or show detailed error
                    UIHelpers.SetError(ErrorText, "An error occurred: " + ex.Message);
                }
                finally
                {
                    // Re-enable button in case of error
                    FinalizeButton.Item1.IsEnabled = true;
                    FinalizeButton.Item1.Content = "Finalize Project";
                }
            };


        }


        public void CreateIndex(SessionStorage.TableObject refTable, SessionStorage.IndexCreation? indexInfo)
        {

            //TableName;




            Tabs.Items.Clear();

            if (indexInfo == null)
            {
                indexInfo = new SessionStorage.IndexCreation();
            }


            var indexData = (SessionStorage.IndexCreation)indexInfo;

            var indexHistoryItem = indexData;

            bool originalIsNull = indexInfo == null;

            indexData.TableName = refTable.SchemaName + "." + refTable.TableName;

            //Remember the schema will automatically have the "" around the SchemaName when required


            // Create a basicsPanel for the index type selection UI
            var basicsPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var basicsTab = new TabItem { Header = "Basics", Content = basicsPanel };
            Tabs.Items.Add(basicsTab);

            basicsPanel.Children.Add(UIHelpers.CreateBasicTitle("Index Name"));
            basicsPanel.Children.Add(UIHelpers.CreateSubTitle("What Is The Name Of This Index? You can optionally leave this empty!"));

            var indexNameField = UIHelpers.CreateTextInput("You Can Leave This Empty For A Randomly Generated ID For A Name!");

            var ErrorText = UIHelpers.CreateError();


            if (indexData.ColumnNames == null)
            {
                indexData.ColumnNames = new List<string>();
            }

            indexNameField.Item1.Text = "";

            if (indexData.IndexName != null)
            {
                indexNameField.Item1.Text = indexData.IndexName;
            }

            //100 vs 10000 IQ gameplay
            indexNameField.Item1.TextChanged += (s, e) =>
            {
                indexData.IndexName = indexNameField.Item1.Text;

                if (indexNameField.Item1.Text != null)
                {
                    bool nameIsUsed = false;

                    foreach (var item in refTable.Indexes)
                    {
                        if (item.IndexName == indexData.IndexName)
                        {
                            nameIsUsed = true;
                            break;
                        }
                    }
                    
                    if (nameIsUsed)
                    {
                        UIHelpers.SetError(ErrorText, "This Index Name Has Been Used Already, Please Use Another!");
                    }
                    else
                    {
                        UIHelpers.ResetError(ErrorText);
                    }
                        
                }
                else
                {
                    UIHelpers.ResetError(ErrorText);
                }



            };

            basicsPanel.Children.Add(indexNameField.Item2);
            basicsPanel.Children.Add(ErrorText);

            basicsPanel.Children.Add(UIHelpers.CreateBasicTitle("What Index Type Is This?"));
            basicsPanel.Children.Add(UIHelpers.CreateSubTitle("How Do We Catalogue The Type Of Data?"));

            var idxBtn1 = UIHelpers.CreateBasicButton("Basic (BTree) - Supports One Column");
            var idxBtn2 = UIHelpers.CreateBasicButton("Composite - Supports Multiple Columns");
            var idxBtn3 = UIHelpers.CreateBasicButton("Partial - Supports One Column");
            var idxBtn4 = UIHelpers.CreateBasicButton("Expression - Supports One Column");
            var idxBtn5 = UIHelpers.CreateBasicButton("Gin - Supports One Column");
            var idxBtn6 = UIHelpers.CreateBasicButton("Unique - Supports One Column");
            var idxBtn7 = UIHelpers.CreateBasicButton("Hash - Supports One Column");
            var idxBtn8 = UIHelpers.CreateBasicButton("Custom - Supports Multiple Columns (Use with Caution!)");


            basicsPanel.Children.Add(idxBtn1.Item2);
            basicsPanel.Children.Add(idxBtn2.Item2);
            basicsPanel.Children.Add(idxBtn3.Item2);
            basicsPanel.Children.Add(idxBtn4.Item2);
            basicsPanel.Children.Add(idxBtn5.Item2);
            basicsPanel.Children.Add(idxBtn6.Item2);
            basicsPanel.Children.Add(idxBtn7.Item2);
            basicsPanel.Children.Add(idxBtn8.Item2);

            var indexButtons = new List<Button> { idxBtn1.Item1, idxBtn2.Item1, idxBtn3.Item1, idxBtn4.Item1, idxBtn5.Item1, idxBtn6.Item1, idxBtn7.Item1, idxBtn8.Item1 };
            Button highlightedIndexType = null;

            // Default selection
            if (string.IsNullOrEmpty(indexData.IndexType))
            {
                indexData.IndexType = "Basic";
                highlightedIndexType = idxBtn1.Item1;
            }

            else
            {
                switch (indexData.IndexType.ToUpperInvariant())
                {
                    case "Basic": highlightedIndexType = idxBtn1.Item1; break; // Basic
                    case "Composite": highlightedIndexType = idxBtn2.Item1; break; // Composite
                    case "Partial": highlightedIndexType = idxBtn3.Item1; break; // Partial
                    case "Expression": highlightedIndexType = idxBtn4.Item1; break; // Expression
                    case "Gin": highlightedIndexType = idxBtn5.Item1; break; // Gin
                    case "Unique": highlightedIndexType = idxBtn6.Item1; break; // Unique
                    case "Hash": highlightedIndexType = idxBtn7.Item1; break; // Hash
                    case "Custom": highlightedIndexType = idxBtn8.Item1; break; // Custom
                    default: highlightedIndexType = idxBtn1.Item1; break; // Default to Basic
                }
            };

        UIHelpers.EnableHighlighting(indexButtons, highlightedIndexType);

            // Custom type input box
            var customBoxTuple = UIHelpers.CreateTextInput("Custom Index Type");
            var customBox = customBoxTuple.Item1;
            var customBoxPanel = customBoxTuple.Item2;
            customBoxPanel.Visibility = indexData.IndexType == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            basicsPanel.Children.Add(customBoxPanel);

            if (indexData.IndexTypeCustom != null)
            {
                customBox.Text = indexData.IndexTypeCustom;
            }
            else
            {
                customBox.Text = "";
            }

                customBox.TextChanged += (s, e) => indexData.IndexTypeCustom = customBox.Text;

            // Button Click Handlers
            idxBtn1.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn1.Item1);
                indexData.IndexType = "BTREE";         // Basic
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn2.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn2.Item1);
                indexData.IndexType = "COMPOSITE";    // Composite
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn3.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn3.Item1);
                indexData.IndexType = "PARTIAL";      // Partial
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn4.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn4.Item1);
                indexData.IndexType = "EXPRESSION";   // Expression
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn5.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn5.Item1);
                indexData.IndexType = "GIN";          // Gin
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn6.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn6.Item1);
                indexData.IndexType = "UNIQUE";       // Unique
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn7.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn7.Item1);
                indexData.IndexType = "HASH";         // Hash
                customBoxPanel.Visibility = Visibility.Collapsed;
            };

            idxBtn8.Item1.Click += (s, e) =>
            {
                UIHelpers.EnableHighlighting(indexButtons, idxBtn8.Item1);
                indexData.IndexType = "CUSTOM";       // Custom
                customBoxPanel.Visibility = Visibility.Visible;
            };



            var usesJsonBPathTitle = UIHelpers.CreateBasicTitle("Does this use JsonBPath?");
            var jsobBPathSubtitle = UIHelpers.CreateSubTitle("This significantly speeds up queries that use JSONPath expressions! Use wisely.");

            var usesJsonBPathYes = UIHelpers.CreateBasicButton("Yes");
            var usesJsonBPathNo = UIHelpers.CreateBasicButton("No");

            if (indexData.IndexType == "GIN" || indexData.IndexType == "CUSTOM")
            {
                usesJsonBPathTitle.Visibility = Visibility.Visible;
                jsobBPathSubtitle.Visibility = Visibility.Visible;
                usesJsonBPathYes.Item1.Visibility = Visibility.Visible;
                usesJsonBPathNo.Item1.Visibility = Visibility.Visible;

            }

            else
            {
                usesJsonBPathTitle.Visibility = Visibility.Collapsed;
                jsobBPathSubtitle.Visibility = Visibility.Collapsed;
                usesJsonBPathYes.Item1.Visibility = Visibility.Collapsed;
                usesJsonBPathNo.Item1.Visibility = Visibility.Collapsed;

            }

            Button? SelectedItem = null;

            if (indexData.UseJsonbPathOps != null)
            {
                if (indexData.UseJsonbPathOps == true)
                {
                    SelectedItem = usesJsonBPathYes.Item1;
                }
                else
                {
                    SelectedItem = usesJsonBPathNo.Item1;
                }

            }

            UIHelpers.EnableHighlighting(new List<Button> { usesJsonBPathYes.Item1, usesJsonBPathNo.Item1 }, SelectedItem);

            usesJsonBPathYes.Item1.Click += (s, e) =>
            {
                indexData.UseJsonbPathOps = true;
            };

            usesJsonBPathNo.Item1.Click += (s, e) =>
            {
                indexData.UseJsonbPathOps = false;
            };

            #region Columns

            // Create a column selection screen for the index type selection UI
            var columnsPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent)
            };

            var columnsTab = new TabItem { Header = "Columns Affected", Content = columnsPanel };
            Tabs.Items.Add(columnsTab);

            columnsPanel.Children.Add(UIHelpers.CreateBasicTitle("Affected Columns"));
            columnsPanel.Children.Add(UIHelpers.CreateSubTitle("What Columns Use This Indexing Type?"));

            var selectColumn = UIHelpers.CreateSubTitle("Selected Columns: ");
            columnsPanel.Children.Add(selectColumn);


            ObservableCollection<string> ColumnNames = new ObservableCollection<string>();
            List<Button> Buttons = new List<Button>();


            foreach (var item in refTable.Rows)
            {
                var btn = UIHelpers.CreateBasicButton(item.Name);
                columnsPanel.Children.Add(btn.Item2);
                Buttons.Add(btn.Item1);


                btn.Item1.Click += (s, e) =>
                {
                    if (indexData.IndexType == "Composite" || indexData.IndexType == "Custom")
                    {
                        if (ColumnNames.Contains(item.Name))
                        {
                            ColumnNames.Remove(item.Name);
                        }
                        else
                        {
                            ColumnNames.Add(item.Name);
                        }
                    }

                    else
                    {
                        if (ColumnNames.Contains(item.Name))
                        {
                            ColumnNames.Clear();
                        }
                        else
                        {
                            ColumnNames.Clear();
                            ColumnNames.Add(item.Name);
                        }

                    }

                    string finalText = "Selected Columns: ";

                    foreach (var name in ColumnNames)
                    {
                        if (ColumnNames.Last() == name)
                        {
                            finalText += $" {name}";

                        }

                        else
                        {
                            finalText += $" {name},";
                        }

                    }

                    selectColumn.Text = finalText;
                };





            }

            if (indexData.ColumnNames != null)
            {
                foreach (var text in indexData.ColumnNames)
                {
                    ColumnNames.Add(text);


                    string finalText = "Selected Columns: ";

                    foreach (var name in ColumnNames)
                    {
                        if (ColumnNames.Last() == name)
                        {
                            finalText += $" {name}";

                        }

                        else
                        {
                            finalText += $" {name},";
                        }

                    }

                    selectColumn.Text = finalText;
                }
            }

            ColumnNames.CollectionChanged += (s, e) =>
            {
                indexData.ColumnNames.Clear();
                foreach (var columnName in ColumnNames)
                {
                    indexData.ColumnNames.Add(columnName);
                }
            };

            #endregion


            #region Advanced Tab
            // Create a scrollable stack panel for advanced options
            var advTab = new TabItem { Header = "Advanced" };
            var advPanel = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush(Colors.Transparent)
                },
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };
            advTab.Content = advPanel;
            Tabs.Items.Add(advTab);

            var advStackPanel = (StackPanel)advPanel.Content;
            advStackPanel.Children.Add(UIHelpers.CreateBasicTitle("Advanced Options"));
            advStackPanel.Children.Add(UIHelpers.CreateSubTitle("Are There Any Extra Expressions or Conditions?"));

            // Condition input
            var conditionTuple = UIHelpers.CreateTextInput("Optional Condition (WHERE ...)");
            var conditionBox = conditionTuple.Item1;
            conditionBox.Text = "";
            advStackPanel.Children.Add(conditionTuple.Item2);
            conditionBox.TextChanged += (s, e) => indexData.Condition = conditionBox.Text;

            // Expression input
            var exprTuple = UIHelpers.CreateTextInput("Optional Expression");
            var errorExp = UIHelpers.CreateError();
            advStackPanel.Children.Add(errorExp);
            var exprBox = exprTuple.Item1;
            exprBox.Text = "";
            advStackPanel.Children.Add(exprTuple.Item2);
            exprBox.TextChanged += (s, e) => indexData.Expression = exprBox.Text;


            if (indexData.Condition != null)
            {
                conditionTuple.Item1.Text = indexData.Condition;

            }

            if (indexData.Expression != null)
            {
                exprTuple.Item1.Text = indexData.Expression;

            }

            exprBox.TextChanged += (s, e) =>
            {
                string expr = exprBox.Text?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(expr))
                {
                    UIHelpers.ResetError(errorExp);
                    return;
                }

                if (!Regex.IsMatch(expr, @"^\s*\(.*\)\s*$"))
                {
                    UIHelpers.SetError(errorExp, "Expression should be wrapped in parentheses, e.g. (age > 18) or ((age > 18) AND active)");
                    return;
                }

                UIHelpers.ResetError(errorExp);
            };


            #endregion

            #region Finalize Tab
            var finalizeTab = new TabItem { Header = "Finalize" };
            var finalizePanel = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush(Colors.Transparent)
                },
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 1270,
                Height = 808
            };
            finalizeTab.Content = finalizePanel;
            Tabs.Items.Add(finalizeTab);

            var finalizeStackPanel = (StackPanel)finalizePanel.Content;


            Tabs.SelectionChanged += (s, e) =>
            {

                if (Tabs.SelectedItem != finalizeTab) return;

                finalizeStackPanel.Children.Clear();
                finalizeStackPanel.Children.Add(UIHelpers.CreateBasicTitle("Finalize Index"));

                finalizeStackPanel.Children.Add(UIHelpers.CreateParagraph($"Table: {refTable.TableName}"));
                finalizeStackPanel.Children.Add(UIHelpers.CreateParagraph($"Columns: {string.Join(", ", indexData.ColumnNames ?? new List<string>())}"));
                finalizeStackPanel.Children.Add(UIHelpers.CreateParagraph($"Index Type: {indexData.IndexType} {(indexData.IndexType == "Custom" ? indexData.IndexTypeCustom : "")}"));
                if (!string.IsNullOrEmpty(indexData.Condition))
                    finalizeStackPanel.Children.Add(UIHelpers.CreateParagraph($"Condition: {indexData.Condition}"));
                if (!string.IsNullOrEmpty(indexData.Expression))
                    finalizeStackPanel.Children.Add(UIHelpers.CreateParagraph($"Expression: {indexData.Expression}"));

                var error = UIHelpers.CreateError();
                finalizeStackPanel.Children.Add(error);



                var finalizeBtn = UIHelpers.CreateBasicButton("Finalize");
                finalizeBtn.Item1.Visibility = Visibility.Collapsed;

                finalizeStackPanel.Children.Add(finalizeBtn.Item2);

                if (string.IsNullOrWhiteSpace(indexData.IndexType) || indexData.ColumnNames == null || !indexData.ColumnNames.Any())
                {
                    UIHelpers.SetError(error, "You must specify an index type and at least one column.");
                    return;
                }

                if (indexNameField.Item1.Text != null)
                {
                    bool nameIsUsed = false;

                    foreach (var item in refTable.Indexes)
                    {
                        if (item.IndexName == indexData.IndexName)
                        {
                            nameIsUsed = true;
                            break;
                        }
                    }

                    if (nameIsUsed)
                    {
                        UIHelpers.SetError(error, "This Index Name Has Been Used Already, Please Use Another!");
                    }
                    else
                    {
                        UIHelpers.ResetError(error);
                    }

                }

                finalizeBtn.Item1.Visibility = Visibility.Visible;


                finalizeBtn.Item1.Click += (s2, e2) =>
                {


                    var SessionTable = mainPage.MainSessionInfo.Tables.SingleOrDefault(s => s.GetHashCode() == refTable.GetHashCode());

                    if (indexData.IndexType != "Gin" || indexData.IndexType != "Custom")
                    {
                        indexData.IndexTypeCustom = null;
                    }


                    int index;

                    if (originalIsNull || SessionTable.Indexes == null)
                    {
                        index = -1;
                    }
                    else
                    {
                        index = SessionTable.Indexes.IndexOf((IndexCreation)indexHistoryItem);
                    }


                    // Add or replace
                    if (index == -1)
                    {
                        SessionTable.Indexes.Add(indexData);

                    }
                    else
                    {

                        SessionTable.Indexes.RemoveAt(index);
                        SessionTable.Indexes.Insert(index, indexData);

                    }




                    mainPage.ForceCollectionChangeUpate();
                    DBViewer.PopulatePage();


                    if (DBViewer != null)
                    {
                        DBViewer.selectedItem = indexData;
                        DBViewer.LoadItemView(indexData);
                    }

                    Step.Visibility = Visibility.Collapsed;
                    Finalized.Visibility = Visibility.Visible;
                };
            };

            #endregion
        


        }



        public void CloseUIElement(UIElement element)
        {
            var parent = VisualTreeHelper.GetParent(element) as Panel;
            if (parent != null)
            {
                parent.Children.Remove(element);
            }
        }


        private void BasicDatabaseDesigner_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }















        public static class UIHelpers
        {
            public static (ComboBox, StackPanel) CreateDropdown(IEnumerable<object> options, string labelText = "")
            {
                var stackPanel = new StackPanel
                {
                    MaxWidth = 800,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(50, 20, 0, 20),
                };

                var labelRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MaxWidth = 800,
                    Margin = new Thickness(0, 10, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                labelRow.Children.Add(new Image
                {
                    Margin = new Thickness(6, 0, 0, 0),
                    MaxHeight = 40,
                    MaxWidth = 40,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative))
                });

                labelRow.Children.Add(new TextBlock
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Margin = new Thickness(10, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 20,
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = labelText
                });

                var comboBox = new ComboBox
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    MinHeight = 60,
                    MinWidth = 300,
                    Background = new SolidColorBrush(Colors.White),
                    Foreground = new SolidColorBrush(Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    FontSize = 20
                };

                foreach (var option in options)
                {
                    if (option is string textOption)
                    {
                        comboBox.Items.Add(new ComboBoxItem
                        {
                            Content = textOption,
                            Foreground = new SolidColorBrush(Colors.Black)
                        });
                    }
                    else if (option is UIElement uiElementOption)
                    {
                        comboBox.Items.Add(new ComboBoxItem
                        {
                            Content = uiElementOption,
                            Foreground = new SolidColorBrush(Colors.Black)
                        });
                    }
                }

                stackPanel.Children.Add(labelRow);
                stackPanel.Children.Add(comboBox);

                // Add extra margin for better visibility  
                stackPanel.Margin = new Thickness(50, 40, 0, 40);

                return (comboBox, stackPanel);
            }


            public static TextBlock CreateBasicTitle(string text)
            {
                return new TextBlock
                {
                    Margin = new Thickness(50, 15, 0, 0),
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = text,
                };
            }

            public static TextBlock CreateSubTitle(string text)
            {
                return new TextBlock
                {
                    Margin = new Thickness(50, 15, 0, 0),
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = text,
                };
            }
            public static TextBlock CreateParagraph(string text)
            {
                return new TextBlock
                {
                    Margin = new Thickness(50, 15, 0, 0),
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = text,
                };
            }

            public static TextBlock CreateError()
            {
                return new TextBlock
                {
                    Margin = new Thickness(50, 15, 0, 0),
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Foreground = new SolidColorBrush(Colors.Red),
                    Name = "BasicDatapageTitle",
                    Visibility = Visibility.Collapsed
                };
            }

            public static void ResetError(TextBlock ErrorText)
            {
                ErrorText.Text = "";
                ErrorText.Visibility = Visibility.Collapsed;
            }

            public static void SetError(TextBlock ErrorText, string Error)
            {
                ErrorText.Visibility = Visibility.Visible;
                ErrorText.Text = Error;
            }

            public static void EnableHighlighting(IEnumerable<Button> buttons, Button? defaultSelected = null)
            {
                var defaultBrush = new SolidColorBrush(Color.FromArgb(255, 69, 65, 56));
                var highlightBrush = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100));

                foreach (var btn in buttons)
                {
                    btn.Background = btn == defaultSelected ? highlightBrush : defaultBrush;

                    btn.Click += (sender, args) =>
                    {
                        var clicked = (Button)sender;

                        foreach (var other in buttons)
                        {
                            other.Background = other == clicked ? highlightBrush : defaultBrush;
                        }
                    };
                }
            }

            public static void ResetHighlight(IEnumerable<Button> buttons)
            {
                var defaultBrush = new SolidColorBrush(Color.FromArgb(255, 69, 65, 56));

                foreach (var btn in buttons)
                {
                    btn.Background = defaultBrush;
                }
            }


            public static (Button, Grid) CreateBasicButton(string buttonText)
            {
                var grid = new Grid
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 69, 65, 56)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(50, 15, 0, 15),
                };

                var buttonContent = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                buttonContent.Children.Add(new Image
                {
                    Margin = new Thickness(6, 0, 0, 0),
                    MaxHeight = 40,
                    MaxWidth = 40,
                    VerticalAlignment = VerticalAlignment.Center,
                    Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative))
                });

                buttonContent.Children.Add(new TextBlock
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Margin = new Thickness(10, 0, 30, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = buttonText,
                });

                var button = new Button
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 69, 65, 56)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Padding = new Thickness(10),
                    Content = buttonContent
                };

                grid.Children.Add(button);
                return (button, grid);
            }

            public static (TextBox, Button, StackPanel)CreateTextInputWithButton(string labelText)
            {
                var stackPanel = new StackPanel
                {
                    MaxWidth = 680,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(50, 15, 0, 15)
                };

                var labelRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MaxWidth = 680,
                    Margin = new Thickness(0, 10, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                labelRow.Children.Add(new Image
                {
                    Margin = new Thickness(6, 0, 0, 0),
                    MaxHeight = 40,
                    MaxWidth = 40,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative))
                });

                labelRow.Children.Add(new TextBlock
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Margin = new Thickness(10, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = labelText
                });

                var textbox = new TextBox
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    MinHeight = 80,
                    MinWidth = 250,
                    Background = new SolidColorBrush(Colors.White),
                    Text = "TextBox",
                    CaretIndex = 6,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(15, 10, 15, 10),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    Foreground = new SolidColorBrush(Colors.Black)
                };

                var button = new Button
                {
                    Margin = new Thickness(0, 20, 40, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Content = "Thing",
                    Background = new SolidColorBrush(Colors.Black),
                    Height = 18,
                    Width = 602,
                    FontSize = 14,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf")
                };

                stackPanel.Children.Add(labelRow);
                stackPanel.Children.Add(textbox);
                stackPanel.Children.Add(button);

                return (textbox, button, stackPanel);
            }


            public static (TextBox, StackPanel) CreateTextInput(string labelText)
            {
                var stackPanel = new StackPanel
                {
                    MaxWidth = 680,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(50, 15, 0, 15)
                };

                var labelRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MaxWidth = 680,
                    Margin = new Thickness(0, 10, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                labelRow.Children.Add(new Image
                {
                    Margin = new Thickness(6, 0, 0, 0),
                    MaxHeight = 40,
                    MaxWidth = 40,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative))
                });

                labelRow.Children.Add(new TextBlock
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Margin = new Thickness(10, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = labelText
                });

                var textbox = new TextBox
                {
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    MinHeight = 80,
                    MinWidth = 250,
                    Background = new SolidColorBrush(Colors.White),
                    Text = "TextBox",
                    CaretIndex = 6,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(15, 10, 15, 10),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    Foreground = new SolidColorBrush(Colors.Black)
                };

                stackPanel.Children.Add(labelRow);
                stackPanel.Children.Add(textbox);

                return (textbox, stackPanel);
            }


            public static string ReadLineWithUnderscores(string input)
            {
                return input.Replace(' ', '_');
            }

            public static void TrimStackPanel(StackPanel panel, int keepCount)
            {
                if (panel == null) return;

                if (keepCount < 0) keepCount = 0;
                if (keepCount >= panel.Children.Count) return;

                for (int i = panel.Children.Count - 1; i >= keepCount; i--)
                {
                    panel.Children.RemoveAt(i);
                }
            }

        }

        public static readonly Dictionary<string, string> PostgresTypeDescriptions = new Dictionary<string, string>
{
    { "BigInt", "64-bit integer. Example: 9223372036854775807" },
    { "BigSerial", "Auto-incrementing 8-byte integer. Example: 1,2,3..." },
    { "Bytea", "Binary data (e.g., files). Example: \\xDEADBEEF" },
    { "Char", "Fixed-length character. Example: 'A'" },
    { "Cidr", "IP network block. Example: '192.168.0.0/24'" },
    { "Circle", "Circle. Example: '<(0,0),5>'" },
    { "Date", "Date only. Example: '2025-05-30'" },
    { "DateRange", "Range of dates. Example: '[2025-01-01,2025-12-31]'" },
    { "Decimal", "Exact decimal (alias of Numeric). Example: 12345.6789" },
    { "Domain", "PostgreSQL domain type. Example: 'positive_integer'" },
    { "DoublePrecision", "64-bit float. Example: 3.1415926535" },
    { "Enum", "PostgreSQL enum type. Example: 'small,medium,large'" },
    { "Int4Range", "Range of 32-bit integers. Example: '[1,10)'" },
    { "Int8Range", "Range of 64-bit integers. Example: '[1,1000000)'" },
    { "IntArray", "Array of integers. Example: '{1,2,3}'" },
    { "Integer", "32-bit integer. Example: 42" },
    { "Interval", "Time interval. Example: '1 day 02:30:00'" },
    { "Inet", "IP address. Example: '192.168.1.1'" },
    { "Json", "Text JSON. Example: '{\"a\":1}'" },
    { "Jsonb", "Binary JSON (indexed). Example: '{\"active\":true}'" },
    { "Line", "Infinite line. Example: '{1,1,2,2}'" },
    { "LSeg", "Line segment. Example: '[(0,0),(1,1)]'" },
    { "MacAddr", "MAC address. Example: '08:00:2b:01:02:03'" },
    { "Money", "Fixed-precision currency. Example: $12.34" },
    { "NumRange", "Range of numeric/decimal values. Example: '[1.5,10.5]'" },
    { "Numeric", "Arbitrary precision numeric. Example: 12345.6789" },
    { "Path", "Geometric path. Example: '[(1,1),(2,2),(3,3)]'" },
    { "Point", "2D point. Example: '(1.5,2.5)'" },
    { "Polygon", "Polygon. Example: '((0,0),(1,0),(1,1),(0,1),(0,0))'" },
    { "Real", "32-bit float. Example: 3.14" },
    { "SecureMedia", "Encrypted file data (custom). Experimental." },
    { "SecureMediaSession", "Encrypted short-lived media (custom). Experimental." },
    { "Serial", "Auto-incrementing 4-byte integer. Example: 1,2,3..." },
    { "SmallInt", "16-bit integer. Example: 1" },
    { "SmallSerial", "Auto-incrementing 2-byte integer. Example: 1,2,3..." },
    { "Text", "Unlimited text. Example: 'Lorem ipsum'" },
    { "TextArray", "Array of text strings. Example: '{\"a\",\"b\"}'" },
    { "Time", "Time only. Example: '14:30:00'" },
    { "TimeTz", "Time with timezone. Example: '14:30:00+02'" },
    { "Timestamp", "Date & time without timezone. Example: '2025-05-30 14:30:00'" },
    { "TimestampTz", "Date & time with timezone. Example: '2025-05-30T14:30:00Z'" },
    { "TsQuery", "Full-text search query. Example: 'cat & dog'" },
    { "TsRange", "Range of timestamps without timezone. Example: '[2025-01-01,2025-12-31]'" },
    { "TsVector", "Full-text search vector. Example: 'a fat cat'" },
    { "TstzRange", "Range of timestamps with timezone. Example: '[2025-01-01T00:00:00Z,2025-12-31T23:59:59Z]'" },
    { "Uuid", "Unique identifier. Example: '550e8400-e29b-41d4-a716-446655440000'" },
    { "UuidArray", "Array of UUIDs. Example: '{550e8400-e29b-41d4-a716-446655440000}'" },
    { "VarChar", "Variable-length string. Example: 'username123'" },
    { "Xml", "XML data. Example: '<note><to>You</to></note>'" }
};






    }



}
