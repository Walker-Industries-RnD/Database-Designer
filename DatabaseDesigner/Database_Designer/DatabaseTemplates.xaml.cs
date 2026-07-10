using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walker.Crypto;
using WISecureData;
using Path = System.IO.Path;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;

namespace Database_Designer
{
    public partial class DatabaseTemplates : Page
    {
        internal BasicDatabaseDesigner Designer;
        MainPage mainPaged;
        public UIWindowEntry WindowInfo { get; private set; }

        public DatabaseTemplates(MainPage mainPaged, BasicDatabaseDesigner BDD, string mainPath)
        {
            this.InitializeComponent();
            Designer = BDD;
            this.mainPaged = mainPaged;

            mainPaged.IntroPage.Children.Remove(this);

            ExitButton.Click += (s, e) =>
            {
                try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); }
                catch (ArgumentOutOfRangeException) { }
            };

            this.Unloaded += (s, e) => RemoveWindow();

            // Load project templates when page is loaded
            Loaded += async (s, e) =>
            {
                await LoadProjectTemplates(mainPath);
            };
        }

        private async Task LoadProjectTemplates(string mainPath)
        {
            var projectTemplatesRoot = Path.Combine(mainPath, mainPaged.SeshUsername.ConvertToString(),"Project Templates");

            Console.WriteLine(mainPath);
            Console.WriteLine(projectTemplatesRoot);

            if (!Directory.Exists(projectTemplatesRoot))
            {
                var noTemplates = new TextBlock
                {
                    Text = "No Project Templates Found",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                };
                ProjectPanel.Children.Add(noTemplates);
                return;
            }

            // Only get actual pack folders — exclude any folder starting with "v"
            var packFolders = Directory.GetDirectories(projectTemplatesRoot)
                .Where(d => !Path.GetFileName(d).StartsWith("v", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!packFolders.Any())
            {
                var noTemplates = new TextBlock
                {
                    Text = "No Project Template Packs Installed",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                };
                ProjectPanel.Children.Add(noTemplates);
                return;
            }

            foreach (var packFolder in packFolders)
            {
                string packName = Path.GetFileName(packFolder);

                // Get all version folders (v1, v2, v10, etc.)
                var versionFolders = Directory.GetDirectories(packFolder)
                    .Where(d =>
                    {
                        string name = Path.GetFileName(d);
                        return name.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                               int.TryParse(name.Substring(1), out _);  // safer than AsSpan
                    })
                    .ToList();

                if (!versionFolders.Any())
                {
                    Console.WriteLine($"No valid version folders in {packName}");
                    continue;
                }

                // Get latest version (highest number)
                string latestVersionPath = versionFolders
                    .OrderByDescending(d => int.Parse(Path.GetFileName(d).Substring(1)))
                    .First();

                // Search for the template file CASE-INSENSITIVELY
                string templateFile = Directory.GetFiles(latestVersionPath)
                    .FirstOrDefault(f => Path.GetFileName(f).Equals("Template.DsgnRowTmplate", StringComparison.OrdinalIgnoreCase));

                if (templateFile == null)
                {
                    Console.WriteLine($"No Template.DsgnRowTmplate found in {latestVersionPath}");
                    continue;
                }

                string content = File.ReadAllText(templateFile);
                Console.WriteLine("Content:\n" + content);

                var json = JsonObject.Parse(content).AsObject();  
                string projectName = json["Name"]?.GetValue<string>() ?? packName;

                


                var templateButton = new Button
                {
                    Content = projectName,
                    Height = 50,
                    Margin = new Thickness(0, 10, 0, 10),
                    FontSize = 18,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                    Background = new SolidColorBrush(Color.FromArgb(255, 220, 216, 192)),
                    Foreground = new SolidColorBrush(Colors.Black),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(20, 0, 0, 0)
                };

                templateButton.Click += (s, e) =>
                {
                    try
                    {

                        var json = JsonObject.Parse(content).AsObject();
                        string projectName = json["Name"]?.GetValue<string>() ?? packName;
                        string overview = json["Overview"]?.GetValue<string>() ?? "No overview provided";
                        string authorName = json["AuthorName"]?.GetValue<string>() ?? "???";
                        string company = json["Company"]?.GetValue<string>();
                        string website = json["Website"]?.GetValue<string>();
                        string license = json["License"]?.GetValue<string>();
                        string note = json["Note"]?.GetValue<string>();

                        MainInfo.Text = projectName;

                        var descriptionBuilder = new StringBuilder();
                        descriptionBuilder.AppendLine(overview);

                        if (!string.IsNullOrEmpty(note))
                        {
                            descriptionBuilder.AppendLine();
                            descriptionBuilder.AppendLine("Note: " + note);
                        }

                        if (!string.IsNullOrEmpty(license))
                        {
                            descriptionBuilder.AppendLine();
                            descriptionBuilder.AppendLine("License: " + license);
                        }

                        if (!string.IsNullOrEmpty(company))
                        {
                            descriptionBuilder.AppendLine();
                            descriptionBuilder.AppendLine("Company: " + company);
                        }

                        if (!string.IsNullOrEmpty(website))
                        {
                            descriptionBuilder.AppendLine();
                            descriptionBuilder.AppendLine("Website: " + website);
                        }

                        Description.Text = descriptionBuilder.ToString();
                        AuthorName.Text = authorName;

                        // Load banner image if exists
                        string bannerPath = Directory.GetFiles(latestVersionPath)
                            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("Banner", StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(bannerPath))
                        {
                            try
                            {
                                byte[] imageBytes = File.ReadAllBytes(bannerPath);
                                var bitmap = new BitmapImage();
                                using (var stream = new MemoryStream(imageBytes))
                                {
                                    bitmap.SetSource(stream);
                                }
                                PreviewImg.Source = bitmap;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to load banner: {ex.Message}");
                            }
                        }

                        // Load author image (PFP) if exists
                        string pfpPath = Directory.GetFiles(latestVersionPath)
                            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("PFP", StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(pfpPath))
                        {
                            try
                            {
                                byte[] imageBytes = File.ReadAllBytes(pfpPath);
                                var bitmap = new BitmapImage();
                                using (var stream = new MemoryStream(imageBytes))
                                {
                                    bitmap.SetSource(stream);
                                }
                                AuthorImg.Source = bitmap;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to load PFP: {ex.Message}");
                            }
                        }



                        var tablesArray = json["Data"]?.AsArray();
                        if (tablesArray == null)
                        {
                            MessageBox.Show("Template data is missing or corrupted.");
                            return;
                        }

// Parse tables directly from JSON (no encryption)
                        var tables = new List<(string fullTableName, string description, List<RowOptions> rows, List<Reference.ReferenceOptions> references, List<IndexDefinition> indexes)>();

                        foreach (var tableItem in tablesArray)
                        {
                            var tableObj = tableItem.AsObject();
                            string tableName = tableObj["TableName"]?.GetValue<string>() ?? "UntitledTable";
                            string schemaName = tableObj["SchemaName"]?.GetValue<string>() ?? "public";
                            string fullTableName = $"{schemaName}.{tableName}";
                            string description = tableObj["Description"]?.GetValue<string>() ?? "";

                            var rows = new List<RowOptions>();
                            var rowsArray = tableObj["Rows"]?.AsArray();
                            if (rowsArray != null)
                            {
                                foreach (var rowItem in rowsArray)
                                {
                                    var rowObj = rowItem.AsObject();
                                    var limitStr = rowObj["Limit"]?.GetValue<string>();
                                    int? limit = null;
                                    if (!string.IsNullOrEmpty(limitStr) && int.TryParse(limitStr, out var l)) limit = l;
                                    rows.Add(new RowOptions(
                                        fieldName: rowObj["Name"]?.GetValue<string>() ?? "field",
                                        description: rowObj["Description"]?.GetValue<string>() ?? "",
                                        postgresType: Enum.TryParse<PostgresType>(rowObj["RowType"]?.GetValue<string>(), out var pt) ? pt : PostgresType.VarChar,
                                        customType: "",
                                        elementLimit: limit,
                                        isArray: rowObj["IsArray"]?.GetValue<bool>() ?? false,
                                        arrayLimit: int.TryParse(rowObj["ArrayLimit"]?.GetValue<string>(), out var al) ? al : null,
                                        isEncrypted: rowObj["EncryptedAndNOTMedia"]?.GetValue<bool>() ?? false,
                                        isMedia: rowObj["Media"]?.GetValue<bool>() ?? false,
                                        isPrimary: rowObj["IsPrimary"]?.GetValue<bool>() ?? false,
                                        isUnique: rowObj["IsUnique"]?.GetValue<bool>() ?? false,
                                        isNotNull: rowObj["IsNotNull"]?.GetValue<bool>() ?? false,
                                        defaultValue: rowObj["DefaultValue"]?.GetValue<string>(),
                                        check: rowObj["Check"]?.GetValue<string>(),
                                        defaultIsKeyword: rowObj["DefaultIsPostgresFunction"]?.GetValue<bool>() ?? false
                                    ));
                                }
                            }

                            var references = new List<Reference.ReferenceOptions>();
                            var refsArray = tableObj["References"]?.AsArray();
                            if (refsArray != null)
                            {
                                foreach (var refItem in refsArray)
                                {
                                    var refObj = refItem.AsObject();
                                    references.Add(new Reference.ReferenceOptions(
                                        refObj["MainTable"]?.GetValue<string>(),
                                        refObj["RefTable"]?.GetValue<string>(),
                                        refObj["ForeignKey"]?.GetValue<string>(),
                                        refObj["RefTableKey"]?.GetValue<string>(),
                                        Enum.TryParse<ReferentialAction>(refObj["OnDeleteAction"]?.GetValue<string>(), true, out var oda) ? oda : ReferentialAction.NoAction,
                                        Enum.TryParse<ReferentialAction>(refObj["OnUpdateAction"]?.GetValue<string>(), true, out var oua) ? oua : ReferentialAction.NoAction
                                    ));
                                }
                            }

                            var indexes = new List<IndexDefinition>();
                            var idxArray = tableObj["Indexes"]?.AsArray();
                            if (idxArray != null)
                            {
                                foreach (var idxItem in idxArray)
                                {
                                    var idxObj = idxItem.AsObject();
                                    var colNames = new List<string>();
                                    var cols = idxObj["ColumnNames"]?.AsArray();
                                    if (cols != null)
                                        foreach (var c in cols)
                                            colNames.Add(c.GetValue<string>());
                                    indexes.Add(new IndexDefinition(
                                        idxObj["TableName"]?.GetValue<string>() ?? fullTableName,
                                        idxObj["IndexName"]?.GetValue<string>() ?? $"{tableName}_idx",
                                        colNames.ToArray(),
                                        Enum.TryParse<IndexType>(idxObj["IndexType"]?.GetValue<string>(), true, out var it) ? it : IndexType.Basic,
                                        idxObj["Condition"]?.GetValue<string>() ?? "",
                                        idxObj["Expression"]?.GetValue<string>() ?? "",
                                        idxObj["IndexTypeCustom"]?.GetValue<string>() ?? "",
                                        idxObj["UseJsonbPathOps"]?.GetValue<bool>() ?? false
                                    ));
                                }
                            }

                            tables.Add((fullTableName, description, rows, references, indexes));
                        }

                        // Extract RLS, API JSON and session logo from root level
                        string rlsJson = json["rlsJson"]?.GetValue<string>() ?? "";
                        string apiJson = json["apiJson"]?.GetValue<string>() ?? "";
                        string sessionLogo = json["sessionLogo"]?.GetValue<string>();

                        var session = new SessionStorage.DBDesignerSession
                        {
                            SessionName = projectName,
                            SessionDescription = overview ?? "",
                            SessionLogo = sessionLogo,
                            LastEdited = DateTime.Now,
                            Tables = new System.Collections.ObjectModel.ObservableCollection<TableObject>()
                        };

                        foreach (var (ftn, desc, r, refs, idx) in tables)
                        {
                            var tableObj = new TableObject
                            {
                                SchemaName = ftn.Split('.')[0],
                                TableName = ftn.Split('.').Length > 1 ? ftn.Split('.')[1] : ftn,
                                Description = desc,
                                Rows = r.Select(rowOpt => new RowCreation
                                {
                                    Name = rowOpt.FieldName,
                                    Description = rowOpt.Description,
                                    RowType = rowOpt.PostgresType,
                                    Limit = rowOpt.Limit,
                                    IsArray = rowOpt.IsArray,
                                    ArrayLimit = rowOpt.ArrayLimit?.ToString(),
                                    EncryptedAndNOTMedia = rowOpt.IsEncrypted,
                                    Media = rowOpt.IsMedia,
                                    IsPrimary = rowOpt.IsPrimary,
                                    IsUnique = rowOpt.IsUnique,
                                    IsNotNull = rowOpt.IsNotNull,
                                    DefaultValue = rowOpt.DefaultValue,
                                    Check = rowOpt.Check,
                                    DefaultIsPostgresFunction = rowOpt.DefaultIsKeyword
                                }).ToList(),
                                References = refs?.Select(x => new SessionStorage.ReferenceOptions(
                                    x.MainTable,
                                    x.RefTable,
                                    x.ForeignKey,
                                    x.RefTableKey,
                                    x.OnDeleteAction,
                                    x.OnUpdateAction
                                )).ToList(),
                                Indexes = idx?.Select(x => new IndexCreation
                                {
                                    TableName = x.TableName ?? "",
                                    IndexName = x.IndexName ?? "",
                                    ColumnNames = x.ColumnNames?.ToList() ?? new List<string>(),
                                    IndexType = x.IndexType.ToString(),
                                    Condition = x.Condition ?? "",
                                    Expression = x.Expression ?? "",
                                    IndexTypeCustom = x.IndexTypeCustom ?? "",
                                    UseJsonbPathOps = x.UseJsonbPathOps
                                }).ToList()
                            };

                            session.Tables.Add(tableObj);
                        }

                        if (Designer.TemplatedItem.Count >= 1)
                        {
                            Designer.TemplatedItem.Clear();
                        }

                        Designer.TemplateName = projectName;
                        Designer.AuthorName = authorName;
                        Designer.TemplatedItem.Add(session);

                        var scriptsSource = Path.Combine(latestVersionPath, "Scripts");
                        Designer.TemplateScriptsSource = Directory.Exists(scriptsSource) ? scriptsSource : null;

                        // Set RLS and API JSON on mainPage
                        mainPaged.RLSJson = rlsJson;
                        mainPaged.APIJson = apiJson;



                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to load project template: {ex.Message}\n{ex.StackTrace}");
                    }
                };

                ProjectPanel.Children.Add(templateButton);
            }
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
	}
}