using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static Database_Designer.NodeWalker.NodeWalker.Node;
using NodeSession    = Database_Designer.NodeWalker.NodeWalker.SessionData;
using NodeOperations = Database_Designer.NodeWalker.NodeWalker.Operations;
using NodeCompiler   = Database_Designer.NodeWalker.NodeWalker.Compiler;

namespace Database_Designer.NodeWalker
{
    public partial class NodeWalkerWindow : Page
    {
        private ContextMenu _workspaceContextMenu;
        private Point _lastPanPosition;
        private bool _isPanning;

        public NodeSession.Session CurrentSession { get; private set; }
        private Point _lastContextMenuPosition;
        private Point _lastSidebarAddPosition = new Point(500, 300);

        private List<Category> _nodeLibrary = new();
        private List<NodeControl> _selectedNodes  = new();
        private List<NoteControl> _selectedNotes  = new();
        private List<ChunkControl> _selectedChunks = new();
        private Rectangle _selectionBox;
        private Point _selectionStart;
        private bool _isSelecting;
        private List<BareNode> _clipboard = new();
        private ConnectionControl _selectedConnection;
        private bool _isUpdatingSelection;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private bool _hasUnsavedChanges;

        private bool _isDraggingConnection;
        private NodeControl _dragStartNode;
        private string _dragStartPort;
        private bool _dragStartIsOutput;
        private Line _dragLine;

        // Cumulative pan offset relative to the canvas origin.
        // Node/chunk/note Canvas positions reflect (world + pan). On save, we
        // subtract the offset so persisted coordinates are always in world
        // space (invariant to how the user has panned the view).
        private double _panOffsetX;
        private double _panOffsetY;

        // Save next to the running executable so the file lives with the
        // app rather than in %AppData%. AppDomain.CurrentDomain.BaseDirectory
        // resolves to the .exe's folder under both the desktop host and the
        // OpenSilver browser host.
        // Default fallback location — used when NodeWalker is opened
        // standalone (no DBD host). When embedded inside Database Designer,
        // SessionDir is overridden by ResolveProjectScriptsDir() so each
        // project gets its own `Scripts/` folder under its save directory.
        private static readonly string DefaultSessionDir = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory ?? ".", "NODEWLKR");
        private string SessionDir => ResolveProjectScriptsDir() ?? DefaultSessionDir;
        // The launcher (RLSPolicyCreator / FunctionCreator) sets `this.Tag` to
        // a slug like "RLS_admin_users_can_read"; honour it so each policy /
        // function gets its own session file. Falls back to "Session" so
        // standalone instantiation keeps the old single-file behaviour.
        private string SessionFile =>
            (Tag is string s && !string.IsNullOrWhiteSpace(s)) ? s : "Session";

        // When opened from DBD, drop saves into <project>/Scripts/.
        // Falls back to null if there's no host or we can't compose a path.
        private string ResolveProjectScriptsDir()
        {
            try
            {
                if (HostPage == null) return null;
                var seshDir  = HostPage.SeshDirectory.ConvertToString();
                var seshUser = HostPage.SeshUsername.ConvertToString();
                var project  = HostPage.ProjectName;
                if (string.IsNullOrEmpty(seshDir) ||
                    string.IsNullOrEmpty(seshUser) ||
                    string.IsNullOrEmpty(project)) return null;
                return System.IO.Path.Combine(seshDir, seshUser, "Projects", project, "Scripts");
            }
            catch { return null; }
        }


        // The host DBD MainPage instance, when this window was opened from
        // inside Database Designer. Lets NodeWalker reach project context
        // (table list, save dir, etc.) without taking a hard dependency.
        public Database_Designer.MainPage HostPage { get; }

        public NodeWalkerWindow(Database_Designer.MainPage host) : this()
        {
            HostPage = host;
        }

        // Title-bar buttons added by the user's UI redesign. NodeWalker is
        // hosted via DBD's CreateWindow (which already manages window lifetime),
        // so these forward to the host's window-manager hooks where possible
        // and fall back to a no-op so the build never breaks.
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Walk up to the overlay container DBD created and remove this window.
            DependencyObject p = this;
            while (p != null)
            {
                p = System.Windows.Media.VisualTreeHelper.GetParent(p);
                if (p is Panel host && host.Children.Contains(this))
                {
                    host.Children.Remove(this);
                    return;
                }
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle between the embed size DBD assigned and a "fill the host" size.
            if (HostPage == null) return;
            var hostPanel = HostPage.IntroPage;
            if (hostPanel == null) return;
            if (Math.Abs(this.Width - hostPanel.ActualWidth) < 1 && Math.Abs(this.Height - hostPanel.ActualHeight) < 1)
            {
                // Restore.
                this.Width  = Math.Max(900, hostPanel.ActualWidth  * 0.85);
                this.Height = Math.Max(600, hostPanel.ActualHeight * 0.85);
            }
            else
            {
                // Maximise.
                this.Width  = hostPanel.ActualWidth;
                this.Height = hostPanel.ActualHeight;
                Canvas.SetLeft(this, 0);
                Canvas.SetTop(this, 0);
            }
        }

        public NodeWalkerWindow()
        {
            InitializeComponent();
            CurrentSession = NewSession();
            _nodeLibrary = CreateNodeLibrary();
            SetupSidebar();
            SetupWorkspaceContextMenu();
            SetupGridPanning();
            SetupSelectionBox();
            SetupKeyboardShortcuts();
            SetupToolbarButtons();
            SetupAutoSave();
            SetupConnectionDrag();

            WorkspaceCanvas.Background = Brushes.Transparent;
            WorkspaceCanvas.MouseRightButtonDown += WorkspaceCanvas_MouseRightButtonDown;

            SetupZoom();

            // Inspector value box drives node.Logic for literal-style nodes.
            // (Used to be done by AttachLiteralEditor on the canvas itself.)
            InspectorValueBox.TextChanged += OnInspectorValueChanged;

            // Refresh connections on first layout pass
            WorkspaceCanvas.SizeChanged += (s, e) =>
                Dispatcher.BeginInvoke(new Action(RefreshConnections),
                    System.Windows.Threading.DispatcherPriority.Render);

            // Keep a rectangular clip in sync with the workspace area so
            // dragged nodes / chunks don't render over the sidebar columns.
            // OpenSilver's Grid doesn't honour ClipToBounds, but assigning
            // a Geometry to Clip works.
            WorkspaceClip.SizeChanged += (s, e) =>
            {
                WorkspaceClip.Clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
                };
            };

            Loaded += async (s, e) => await LoadSession();
        }

        private static NodeSession.Session NewSession() => new()
        {
            Name = "New Session",
            Nodes = new HashSet<BareNode>(),
            Connections = new HashSet<Connection>(),
            NodePositions = new Dictionary<string, System.Numerics.Vector3>(),
            Chunk = new List<NodeSession.Chunk>(),
            Notes = new List<NodeSession.Note>(),
            FunctionName = "DoACoolThing",
            IsAsync = false
        };

        private void SetupConnectionDrag()
        {
            WorkspaceCanvas.MouseMove += MainPage_MouseMove;
            WorkspaceCanvas.MouseLeftButtonUp += MainPage_MouseLeftButtonUp;
        }

        // Zoom
        // Each canvas layer gets its own ScaleTransform (sharing one across
        // multiple visuals isn't reliable in OpenSilver). All four are kept
        // in sync by SetZoom so chunks/nodes/connections/notes scale together.
        private double _zoom = 1.0;
        private const double _zoomMin = 0.25;
        private const double _zoomMax = 3.0;
        private ScaleTransform _zChunks, _zWorkspace, _zConnections, _zNotes;

        private void SetupZoom()
        {
            _zChunks      = new ScaleTransform();
            _zWorkspace   = new ScaleTransform();
            _zConnections = new ScaleTransform();
            _zNotes       = new ScaleTransform();
            ChunksCanvas.RenderTransform      = _zChunks;
            WorkspaceCanvas.RenderTransform   = _zWorkspace;
            ConnectionsCanvas.RenderTransform = _zConnections;
            NotesCanvas.RenderTransform       = _zNotes;

            // Ctrl+wheel zooms; plain wheel falls through (default scroll, if any).
            WorkspaceClip.MouseWheel += (s, e) =>
            {
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
                e.Handled = true;
                double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
                SetZoom(_zoom * factor);
            };

            // Keyboard shortcuts: Ctrl+= / Ctrl+- / Ctrl+0
            KeyDown += (s, e) =>
            {
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
                if (e.Key == Key.Add)              { SetZoom(_zoom * 1.1); e.Handled = true; }
                else if (e.Key == Key.Subtract)    { SetZoom(_zoom / 1.1); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { SetZoom(1.0); e.Handled = true; }
            };
        }

        private void SetZoom(double newZoom)
        {
            newZoom = Math.Max(_zoomMin, Math.Min(_zoomMax, newZoom));
            if (Math.Abs(newZoom - _zoom) < 0.001) return;
            _zoom = newZoom;
            _zChunks.ScaleX      = _zChunks.ScaleY      = newZoom;
            _zWorkspace.ScaleX   = _zWorkspace.ScaleY   = newZoom;
            _zConnections.ScaleX = _zConnections.ScaleY = newZoom;
            _zNotes.ScaleX       = _zNotes.ScaleY       = newZoom;
            Dispatcher.BeginInvoke(new Action(RefreshConnections),
                System.Windows.Threading.DispatcherPriority.Render);
        }

        private void MainPage_MouseMove(object sender, MouseEventArgs e)
        {
            // Note: the previous self-heal check used MouseEventArgs.LeftButton
            // which doesn't exist in OpenSilver. Cleanup of a half-finished
            // connection drag is now handled by MainPage_MouseLeftButtonUp and
            // by the LostMouseCapture path inside NodeControl.
            if (_isDraggingConnection && _dragLine != null)
            {
                var pos = e.GetPosition(WorkspaceCanvas);
                _dragLine.X2 = pos.X;
                _dragLine.Y2 = pos.Y;
            }
        }

        private void MainPage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingConnection)
            {
                _isDraggingConnection = false;
                if (_dragLine != null)
                {
                    ConnectionsCanvas.Children.Remove(_dragLine);
                    _dragLine = null;
                }
                _dragStartNode = null;
                WorkspaceCanvas.ReleaseMouseCapture();
            }
        }

        private void SetupAutoSave()
        {
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _autoSaveTimer.Tick += async (s, e) =>
            {
                if (_hasUnsavedChanges) { await SaveSession(); _hasUnsavedChanges = false; }
            };
            _autoSaveTimer.Start();
        }

        private void MarkUnsavedChanges() => _hasUnsavedChanges = true;

        private async System.Threading.Tasks.Task SaveSession()
        {
            try
            {
                UpdateSessionFromCanvas();
                await NodeOperations.SaveSession(CurrentSession, SessionFile, SessionDir);
                System.Diagnostics.Debug.WriteLine($"[SAVE] Session saved to {SessionDir}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SAVE] Failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadSession()
        {
            try
            {
                if (!NodeOperations.CheckIfSessionFileExists(SessionFile, SessionDir))
                {
                    System.Diagnostics.Debug.WriteLine("[LOAD] No existing session file — starting fresh.");
                    return;
                }
                var loaded = await NodeOperations.LoadSession(SessionFile, SessionDir);
                CurrentSession = loaded;
                RebuildCanvasFromSession();
                SyncToolbarFromSession();
                System.Diagnostics.Debug.WriteLine("[LOAD] Session loaded successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD] Failed: {ex.Message}");
            }
        }

        private void UpdateSessionFromCanvas()
        {
            // Connections live on the session itself (added by CreateConnection),
            // so don't wipe them — only nodes/positions are sourced from the
            // visual tree on save.
            CurrentSession.Nodes.Clear();
            CurrentSession.NodePositions.Clear();
            CurrentSession.Chunk ??= new();
            CurrentSession.Notes ??= new();
            CurrentSession.CustomScripts ??= new();
            CurrentSession.Chunk.Clear();
            CurrentSession.Notes.Clear();
            CurrentSession.CustomScripts.Clear();

            foreach (var script in _customScripts)
                CurrentSession.CustomScripts.Add(script);

            // Positions are saved raw (whatever Canvas.Left/Top happens to be).
            // The load path always re-centres the layout in the viewport, so
            // absolute coordinates are arbitrary — only the relative geometry
            // between nodes matters.
            float Norm(double v) => (float)(double.IsNaN(v) ? 0 : v);

            var liveUUIDs = new HashSet<string>();
            foreach (var nc in WorkspaceCanvas.Children.OfType<NodeControl>())
            {
                CurrentSession.Nodes.Add(nc.Data);
                liveUUIDs.Add(nc.Data.UUID);
                CurrentSession.NodePositions[nc.Data.UUID] = new System.Numerics.Vector3(
                    Norm(Canvas.GetLeft(nc)), Norm(Canvas.GetTop(nc)), 0f);
            }

            // Drop any connection whose endpoints aren't on the canvas.
            CurrentSession.Connections.RemoveWhere(c =>
                !liveUUIDs.Contains(c.Node1UUID) || !liveUUIDs.Contains(c.Node2UUID));

            foreach (var chunk in ChunksCanvas.Children.OfType<ChunkControl>())
                CurrentSession.Chunk.Add(new NodeSession.Chunk
                {
                    Name = chunk.ChunkTitle,
                    Left = Norm(Canvas.GetLeft(chunk)),
                    Top  = Norm(Canvas.GetTop(chunk)),
                    Width = (float)chunk.ActualWidth, Height = (float)chunk.ActualHeight,
                    BorderColor = chunk.GetBorderColorValue()
                });

            foreach (var note in NotesCanvas.Children.OfType<NoteControl>())
                CurrentSession.Notes.Add(new NodeSession.Note
                {
                    Description = note.NoteText,
                    Left = Norm(Canvas.GetLeft(note)),
                    Top  = Norm(Canvas.GetTop(note))
                });
        }

        private void RebuildCanvasFromSession()
        {
            // Clear canvas layers
            foreach (var c in WorkspaceCanvas.Children.OfType<NodeControl>().ToList()) WorkspaceCanvas.Children.Remove(c);
            foreach (var c in NotesCanvas.Children.OfType<NoteControl>().ToList()) NotesCanvas.Children.Remove(c);
            foreach (var c in ChunksCanvas.Children.OfType<ChunkControl>().ToList()) ChunksCanvas.Children.Remove(c);
            ConnectionsCanvas.Children.Clear();

            // Clear sidebar-backing state. Without this, every load appends
            // the session's custom scripts to the existing list and the
            // sidebar shows each entry once per load.
            _customScripts.Clear();
            _connViews.Clear();

            // Place every element at whatever raw coordinates were saved,
            // then re-centre the whole layout on the viewport once the canvas
            // has reported its actual size. Pan offset starts at 0 — it only
            // tracks panning since the last load, and isn't persisted.
            _panOffsetX = 0;
            _panOffsetY = 0;

            foreach (var node in CurrentSession.Nodes)
            {
                var vc = new NodeControl();
                vc.LoadFromBareNode(node);
                SetupNodeEvents(vc);
                WorkspaceCanvas.Children.Add(vc);
                if (CurrentSession.NodePositions.TryGetValue(node.UUID, out var pos))
                { Canvas.SetLeft(vc, pos.X); Canvas.SetTop(vc, pos.Y); }

                // Literal editing now lives in the right-side Node Inspector;
                // no inline canvas attachment.
                vc.ValuePreview = BuildValuePreview(node);
            }

            foreach (var cd in CurrentSession.Chunk ?? new())
            {
                var chunk = new ChunkControl();
                chunk.LoadFromChunk(cd);
                chunk.SetInitialSize(cd.Width > 0 ? cd.Width : 620, cd.Height > 0 ? cd.Height : 420);
                SetupChunkEvents(chunk);
                ChunksCanvas.Children.Add(chunk);
                Canvas.SetLeft(chunk, cd.Left); Canvas.SetTop(chunk, cd.Top);
            }

            foreach (var nd in CurrentSession.Notes ?? new())
            {
                var note = new NoteControl();
                note.NoteText = nd.Description ?? "";
                SetupNoteEvents(note);
                NotesCanvas.Children.Add(note);
                Canvas.SetLeft(note, nd.Left); Canvas.SetTop(note, nd.Top);
            }

            FrameAllOnNextLayout();

            foreach (var cs in CurrentSession.CustomScripts ?? new())
                _customScripts.Add(cs);

            // Refresh sidebar so custom nodes appear immediately
            SetupSidebar();

            // Two-pass refresh: Loaded then Background ensures all nodes are measured
            Dispatcher.BeginInvoke(new Action(RefreshConnections),
                System.Windows.Threading.DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(RefreshConnections),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Schedules a one-shot anchor-to-top-left pass that runs after the
        /// canvas has been measured. Used by the load path so saved sessions
        /// always come back in a predictable, visible spot regardless of the
        /// raw coordinates they were saved at.
        /// </summary>
        private void FrameAllOnNextLayout()
        {
            void Run(int retries)
            {
                if (WorkspaceCanvas.ActualWidth < 1 || WorkspaceCanvas.ActualHeight < 1)
                {
                    if (retries <= 0) return;
                    Dispatcher.BeginInvoke(new Action(() => Run(retries - 1)),
                        System.Windows.Threading.DispatcherPriority.Background);
                    return;
                }
                FrameAllNow(centerInViewport: false);
            }
            Dispatcher.BeginInvoke(new Action(() => Run(retries: 30)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Manual "Frame All" / "fit to content". Default behaviour anchors
        /// the layout's bounding-box top-left to a small padding from the
        /// canvas origin (predictable, always visible, matches what most
        /// diagram tools do — Graphviz, Mermaid, Lucid, etc.). Pass
        /// <paramref name="centerInViewport"/> = true to instead centre the
        /// bounding box in the visible canvas (Blender's Numpad-Home style).
        /// </summary>
        private void FrameAllNow(bool centerInViewport = true)
        {
            const double padding = 60;

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            void Track(double x, double y, double w, double h)
            {
                if (double.IsNaN(x) || double.IsNaN(y)) return;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x + w > maxX) maxX = x + w;
                if (y + h > maxY) maxY = y + h;
            }
            foreach (var nc in WorkspaceCanvas.Children.OfType<NodeControl>())
                Track(Canvas.GetLeft(nc), Canvas.GetTop(nc), nc.ActualWidth,  nc.ActualHeight);
            foreach (var cc in ChunksCanvas.Children.OfType<ChunkControl>())
                Track(Canvas.GetLeft(cc), Canvas.GetTop(cc), cc.ActualWidth,  cc.ActualHeight);
            foreach (var nt in NotesCanvas.Children.OfType<NoteControl>())
                Track(Canvas.GetLeft(nt), Canvas.GetTop(nt), nt.ActualWidth,  nt.ActualHeight);

            if (double.IsPositiveInfinity(minX)) return; // nothing to frame

            double dx, dy;
            if (centerInViewport &&
                WorkspaceCanvas.ActualWidth  > 1 &&
                WorkspaceCanvas.ActualHeight > 1)
            {
                double bboxW = maxX - minX;
                double bboxH = maxY - minY;
                dx = (WorkspaceCanvas.ActualWidth  - bboxW) * 0.5 - minX;
                dy = (WorkspaceCanvas.ActualHeight - bboxH) * 0.5 - minY;
            }
            else
            {
                // Top-left anchor with padding — visible regardless of canvas size.
                dx = padding - minX;
                dy = padding - minY;
            }

            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return;

            void Shift(Canvas cv)
            {
                foreach (UIElement child in cv.Children)
                {
                    if (child == _selectionBox) continue;
                    var l = Canvas.GetLeft(child); var t = Canvas.GetTop(child);
                    Canvas.SetLeft(child, (double.IsNaN(l) ? 0 : l) + dx);
                    Canvas.SetTop(child,  (double.IsNaN(t) ? 0 : t) + dy);
                }
            }
            Shift(WorkspaceCanvas);
            Shift(ChunksCanvas);
            Shift(NotesCanvas);

            _panOffsetX += dx;
            _panOffsetY += dy;

            Dispatcher.BeginInvoke(new Action(RefreshConnections),
                System.Windows.Threading.DispatcherPriority.Render);
        }

        private void SetupToolbarButtons()
        {
            BindBtn("RefreshButton",  _ => RefreshAll());
            BindBtn("ReplaceButton",  _ => ShowReplaceDialog());
            BindBtn("ViewCodeButton", _ => ToggleCodeViewer());
            BindBtn("SaveButton",     async _ => await SaveSession());
            BindBtn("LoadButton",     async _ => await LoadSession());
            BindBtn("CreateInputButton",  _ => ShowCreatePortDialog(isInput: false));
            BindBtn("PushOutputButton",   _ => ShowCreatePortDialog(isInput: true));
            BindBtn("FrameButton",        _ => FrameAllNow());

            // Function name + async checkbox bound to CurrentSession
            FunctionNameBox.Text = CurrentSession.FunctionName ?? "DoACoolThing";
            FunctionNameBox.LostFocus += (s, e) =>
            {
                var v = FunctionNameBox.Text?.Trim();
                if (!string.IsNullOrEmpty(v) && v != CurrentSession.FunctionName)
                {
                    CurrentSession.FunctionName = v;
                    MarkUnsavedChanges();
                }
            };
            AsyncCheck.IsChecked = CurrentSession.IsAsync;
            AsyncCheck.Checked   += (s, e) => { CurrentSession.IsAsync = true;  MarkUnsavedChanges(); };
            AsyncCheck.Unchecked += (s, e) => { CurrentSession.IsAsync = false; MarkUnsavedChanges(); };
        }

        // Sync toolbar inputs from CurrentSession (used after Load).
        private void SyncToolbarFromSession()
        {
            if (FunctionNameBox != null)
                FunctionNameBox.Text = CurrentSession.FunctionName ?? "DoACoolThing";
            if (AsyncCheck != null)
                AsyncCheck.IsChecked = CurrentSession.IsAsync;
        }

        private void BindBtn(string name, Func<RoutedEventArgs, System.Threading.Tasks.Task> handler)
        {
            if (FindName(name) is Button btn)
                btn.Click += async (s, e) => await handler(e);
        }
        private void BindBtn(string name, Action<RoutedEventArgs> handler)
        {
            if (FindName(name) is Button btn)
                btn.Click += (s, e) => handler(e);
        }

        private void ToggleCodeViewer()
        {
            if (CodeViewerPanel.Visibility == Visibility.Visible)
                CodeViewerPanel.Visibility = Visibility.Collapsed;
            else
            {
                ShowGeneratedCode();
                CodeViewerPanel.Visibility = Visibility.Visible;
            }
        }

        private void CloseCodeViewer_Click(object sender, RoutedEventArgs e)
            => CodeViewerPanel.Visibility = Visibility.Collapsed;

private void ShowGeneratedCode()
        {
            try
            {
                // Use the full compiler in NodeWalker.cs
                CodeViewerText.Text = NodeCompiler.CompileToScript(CurrentSession);
                // Update line count status bar
                var lines = (CodeViewerText.Text ?? "").Split('\n').Length;
                if (FindName("CodeStatusBar") is TextBlock sb)
                    sb.Text = $"{lines} line{(lines == 1 ? "" : "s")}";
            }
            catch (Exception ex)
            {
                CodeViewerText.Text = $"// Code generation error\n// {ex.Message}\n\n{ex.StackTrace}";
            }
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CodeViewerText?.Text)) return;
            try
            {
                Clipboard.SetText(CodeViewerText.Text);
                if (FindName("CodeStatusBar") is TextBlock sb)
                {
                    var prev = sb.Text;
                    sb.Text = "✓ Copied!";
                    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    t.Tick += (s2, _) => { sb.Text = prev; t.Stop(); };
                    t.Start();
                }
            }
            catch { }
        }

        private static readonly string[] _baseTypes = { "string", "number", "bool", "object", "custom" };

        private void ShowCreatePortDialog(bool isInput)
        {
            // isInput = true → creates "Set Output" node (node that accepts an input port from graph)
            // isInput = false → creates "Event Input" node (node with output ports representing graph inputs)
            var title = isInput ? "Create Set Output Node" : "Create Event Input Node";
            var portLabel = isInput ? "Input Name:" : "Output Name:";
            var accent = isInput ? Color.FromRgb(33, 150, 243) : Color.FromRgb(76, 175, 80);

            ShowMiniDialog(title, dialog =>
            {
                var nameBox = AddLabeledTextBox(dialog, portLabel, isInput ? "output" : "input");
                var (typeBox, customBox) = AddTypeSelector(dialog, "Type:");

                AddButtons(dialog, accent, "Create", () =>
                {
                    var name = nameBox.Text.Trim().Length > 0 ? nameBox.Text.Trim() : (isInput ? "output" : "input");
                    var semantic = GetSelectedSemantic(typeBox, customBox);
                    var t = GetTypeFromSemantic(semantic);

                    BareNode newNode;
                    if (isInput)
                    {
                        newNode = new BareNode
                        {
                            Title = "Set Output",
                            Description = $"Sets {semantic} output",
                            Inputs = new HashSet<Input> { new Input(name, t, semantic, true) },
                            Outputs = new HashSet<Output>(),
                            UUID = Guid.NewGuid().ToString(), Logic = "", SyncType = "Sync"
                        };
                    }
                    else
                    {
                        newNode = new BareNode
                        {
                            Title = "Event Input",
                            Description = $"Triggers with {semantic} value",
                            Inputs = new HashSet<Input>(),
                            Outputs = new HashSet<Output> { new Output(name, t, semantic) },
                            UUID = Guid.NewGuid().ToString(), Logic = "", SyncType = "Sync"
                        };
                    }
                    AddNodeFromTemplateAt(newNode, new Point(100, 200));
                });
            }, accent);
        }

        // Inline "Add Node" popup — Unity Bolt style

        private void ShowAddNodePopup(Point canvasPosition)
        {
            _lastContextMenuPosition = canvasPosition;

            // Build a popup overlay that looks like Unity's Add Node dialog
            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)) };

            var panel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Width = 280,
                MaxHeight = 480,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel();

            // Search header
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(6, 6, 0, 0)
            };

            var searchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(48, 48, 48)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI")
            };

            var watermark = new TextBlock
            {
                Text = "Search nodes...",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                IsHitTestVisible = false,
                FontSize = 13,
                Margin = new Thickness(9, 7, 0, 0),
                FontFamily = new FontFamily("Segoe UI")
            };

            var nodeListPanel = new StackPanel { Margin = new Thickness(4, 4, 4, 4) };
    

            var searchGrid = new Grid();
            searchGrid.Children.Add(searchBox);
            searchGrid.Children.Add(watermark);
            searchBorder.Child = searchGrid;
            searchBox.TextChanged += (s, e) =>
            {
                watermark.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                RefreshNodeList(nodeListPanel, searchBox.Text);
            };

            stack.Children.Add(searchBorder);

            // Node list
            var listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 380
            };

            listScroll.Content = nodeListPanel;
            stack.Children.Add(listScroll);
            panel.Child = stack;

            Action close = () =>
            {
                var parent = overlay.Parent as Grid;
                parent?.Children.Remove(overlay);
            };

            void OnNodeChosen(BareNode node)
            {
                AddNodeFromTemplateAt(node, canvasPosition);
                close();
            }

            RefreshNodeList(nodeListPanel, "", OnNodeChosen);

            overlay.Children.Add(panel);
            overlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == overlay) close();
            };

            searchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) close();
            };

            var mainGrid = this.Content as Grid;
            if (mainGrid != null)
            {
                overlay.SetValue(Grid.RowSpanProperty, 3);
                mainGrid.Children.Add(overlay);
                searchBox.Focus();
            }
        }

        private void RefreshNodeList(StackPanel panel, string filter, Action<BareNode> onSelect = null)
        {
            panel.Children.Clear();
            var lower = filter?.ToLower() ?? "";

            foreach (var cat in _nodeLibrary)
            {
                var matchingNodes = cat.Nodes.Where(n =>
                    string.IsNullOrEmpty(lower) ||
                    n.Title.ToLower().Contains(lower) ||
                    (n.Description?.ToLower().Contains(lower) ?? false)).ToList();

                if (!matchingNodes.Any()) continue;

                // Category header
                var catHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 4, 0, 2),
                    CornerRadius = new CornerRadius(3)
                };
                catHeader.Child = new TextBlock
                {
                    Text = cat.Name.ToUpper(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    FontFamily = new FontFamily("Segoe UI")
                };
                panel.Children.Add(catHeader);

                foreach (var node in matchingNodes)
                {
                    var nodeBtn = new Border
                    {
                        Padding = new Thickness(10, 5, 10, 5),
                        Cursor = Cursors.Hand,
                        CornerRadius = new CornerRadius(3),
                        Margin = new Thickness(2, 1, 2, 1),
                        Tag = node
                    };

                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(new Ellipse
                    {
                        Width = 8, Height = 8,
                        Fill = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    row.Children.Add(new TextBlock
                    {
                        Text = node.Title,
                        FontSize = 13,
                        Foreground = Brushes.White,
                        FontFamily = new FontFamily("Segoe UI"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    nodeBtn.Child = row;

                    nodeBtn.MouseEnter += (s, e) =>
                        nodeBtn.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                    nodeBtn.MouseLeave += (s, e) =>
                        nodeBtn.Background = Brushes.Transparent;
                    nodeBtn.MouseLeftButtonDown += (s, e) =>
                    {
                        if (nodeBtn.Tag is BareNode n) (onSelect ?? (bn => AddNodeFromTemplate(bn)))(n);
                        e.Handled = true;
                    };

                    panel.Children.Add(nodeBtn);
                }
            }
        }

        // Replace dialog — full node-type replacement with port mapping

        private void ShowReplaceDialog()
        {
            UpdateSessionFromCanvas();

            var allNodeTitles = CurrentSession.Nodes
                .Select(n => n.Title)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            ShowMiniDialog("Search & Replace Nodes", dialog =>
            {
                // ROW 1: Find node type
                AddSectionLabel(dialog, "Find All Instances Of:");
                var findTypeBox = new ComboBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                foreach (var t in allNodeTitles) findTypeBox.Items.Add(t);
                if (allNodeTitles.Any()) findTypeBox.SelectedIndex = 0;
                dialog.Children.Add(findTypeBox);

                // ROW 2: Find port
                AddSectionLabel(dialog, "And Get Port:");
                var findPortBox = new ComboBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                dialog.Children.Add(findPortBox);

                findTypeBox.SelectionChanged += (s, e) => RefreshPortList(findPortBox, findTypeBox.SelectedItem?.ToString());

                // ROW 3: Replace with
                AddSectionLabel(dialog, "Then Replace It With:");
                var replaceTypeBox = new ComboBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                foreach (var cat in _nodeLibrary)
                    foreach (var n in cat.Nodes)
                        replaceTypeBox.Items.Add(n.Title);
                if (replaceTypeBox.Items.Count > 0) replaceTypeBox.SelectedIndex = 0;
                dialog.Children.Add(replaceTypeBox);

                // ROW 4: Replace port mapping
                AddSectionLabel(dialog, "'s Value Of (replace port name):");
                var replacePortBox = new TextBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    Padding = new Thickness(8, 5, 8, 5),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 15),
                    Text = ""
                };
                dialog.Children.Add(replacePortBox);

                // Result label
                var resultLabel = new TextBlock
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 100)),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                dialog.Children.Add(resultLabel);

                // Initialise port list
                if (allNodeTitles.Any()) RefreshPortList(findPortBox, allNodeTitles[0]);

                bool RunReplace()
                {
                    var findTitle = findTypeBox.SelectedItem?.ToString();
                    var findPort = findPortBox.SelectedItem?.ToString() ?? "";
                    var replaceTitle = replaceTypeBox.SelectedItem?.ToString();
                    var replacePort = replacePortBox.Text.Trim();

                    if (string.IsNullOrEmpty(findTitle) || string.IsNullOrEmpty(replaceTitle)) return false;

                    var replaceNode = _nodeLibrary.SelectMany(c => c.Nodes)
                        .FirstOrDefault(n => n.Title == replaceTitle);
                    if (replaceNode == null) return false;

                    if (string.IsNullOrEmpty(replacePort)) replacePort = findPort;

                    var result = NodeOperations.ReplaceNodesByTitle(
                        CurrentSession, findTitle, findPort,
                        replaceNode, replacePort);

                    RebuildCanvasFromSession();

                    resultLabel.Text = $"✓ Replaced {result.ReplacedCount} node(s). " +
                        (result.DroppedConnections.Any()
                            ? $"{result.DroppedConnections.Count} connection(s) dropped (incompatible ports)."
                            : "All connections preserved.");
                    MarkUnsavedChanges();
                    return true;
                }

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
                MakeBtn(btnRow, "Go",                 Color.FromRgb(255, 76, 76), () => RunReplace());
                MakeBtn(btnRow, "Do To All & Close",  Color.FromRgb(68, 68, 68),  () => { if (RunReplace()) CloseAllOverlays(); });
                MakeBtn(btnRow, "Cancel",             Color.FromRgb(68, 68, 68),  () => CloseAllOverlays());
                dialog.Children.Add(btnRow);

            }, Color.FromRgb(255, 76, 76), closeOnAction: false);
        }

        private void RefreshPortList(ComboBox portBox, string nodeTitle)
        {
            portBox.Items.Clear();
            if (string.IsNullOrEmpty(nodeTitle)) return;
            var node = CurrentSession.Nodes.FirstOrDefault(n => n.Title == nodeTitle);
            if (node == null) return;
            foreach (var i in node.Inputs) portBox.Items.Add(i.Name);
            foreach (var o in node.Outputs) portBox.Items.Add(o.Name);
            if (portBox.Items.Count > 0) portBox.SelectedIndex = 0;
        }

        private void CheckAndShowWarnings()
        {
            var warnings = NodeSession.ValidateRequiredPorts(CurrentSession);
            foreach (var nc in WorkspaceCanvas.Children.OfType<NodeControl>())
            {
                var nodeWarnings = warnings.Where(w => w.Contains($"\"{nc.Data.Title}\"")).ToList();
                if (nodeWarnings.Any())
                {
                    nc.HasWarning = true;
                    nc.WarningText = string.Join("; ", nodeWarnings.Select(w =>
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(w, @"required input ""([^""]+)""");
                        return m.Success ? $"Required: {m.Groups[1].Value}" : w;
                    }));
                }
                else
                {
                    nc.HasWarning = nc.Data.Logic?.StartsWith("ERROR:") == true;
                    if (nc.HasWarning) nc.WarningText = nc.Data.Logic;
                }
            }
        }

        private void DeleteSelectedNode_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNodes.Count > 0) { DeleteSelectedNodes(); HideNodeInspector(); }
        }

        private NodeControl _inspectorTarget;
        private bool _inspectorValueLoading;

        private void ShowNodeInspector(NodeControl node)
        {
            _inspectorTarget = node;
            InspectorTitle.Text = node.Data.Title ?? "Untitled";
            InspectorDesc.Text  = node.Data.Description ?? "";

            // Value editor — visible for any node that previously got the
            // inline editor. Replaces the on-canvas TextBox: edits route into
            // node.Data.Logic the same way, with the Custom Input port
            // re-typing side effect preserved.
            if (NeedsLiteralEditor(node.Data))
            {
                _inspectorValueLoading = true;
                InspectorValuePanel.Visibility = Visibility.Visible;
                InspectorValueHint.Text = LiteralEditorHint(node.Data.Title);
                InspectorValueBox.AcceptsReturn = LiteralEditorMultiLine(node.Data.Title);
                InspectorValueBox.Height = InspectorValueBox.AcceptsReturn ? 140 : 32;
                InspectorValueBox.Text = ExtractEditorText(node.Data);
                _inspectorValueLoading = false;
            }
            else
            {
                InspectorValuePanel.Visibility = Visibility.Collapsed;
            }

            InspectorInputs.Items.Clear();
            foreach (var inp in node.Data.Inputs)
            {
                var hasConn = CurrentSession.Connections.Any(c => c.Node2UUID == node.Data.UUID && c.Node2Port == inp.Name);
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock
                {
                    Text = $"• {inp.Name}: {inp.SemanticType ?? inp.Type?.Name ?? "object"}{(inp.Required ? " *" : "")}",
                    Foreground = inp.Required && !hasConn ? new SolidColorBrush(Color.FromRgb(255, 150, 150)) : Brushes.White,
                    FontSize = 12
                };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                if (inp.Required && !hasConn)
                {
                    var warn = new TextBlock { Text = "⚠", Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80)), FontSize = 14 };
                    Grid.SetColumn(warn, 1);
                    row.Children.Add(warn);
                }
                InspectorInputs.Items.Add(row);
            }

            InspectorOutputs.Items.Clear();
            foreach (var outp in node.Data.Outputs)
                InspectorOutputs.Items.Add(new TextBlock
                {
                    Text = $"• {outp.Name}: {outp.SemanticType ?? outp.Type?.Name ?? "object"}",
                    Foreground = Brushes.White, FontSize = 12
                });

            NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
            NodeInspector.Visibility = Visibility.Visible;
        }

        private void HideNodeInspector()
        {
            _inspectorTarget = null;
            NodeInspector.Visibility = Visibility.Collapsed;
            NoSelectionPlaceholder.Visibility = Visibility.Visible;
            InspectorValuePanel.Visibility = Visibility.Collapsed;
        }

        // Compose the inline value tag shown on the node body. Returns null
        // when there's nothing useful to show (so the tag row collapses).
        private static string BuildValuePreview(BareNode node)
        {
            if (node?.Title == null) return null;
            var raw = ExtractEditorText(node);
            if (string.IsNullOrEmpty(raw)) return null;

            // Custom Literal stores "<TypeName>\n<body>" — show first 1-2 lines.
            if (node.Title == "Custom Literal")
            {
                var nl = raw.IndexOf('\n');
                if (nl < 0) return raw.Trim();
                var t = raw.Substring(0, nl).Trim();
                var rest = raw.Substring(nl + 1).Replace('\n', ' ').Trim();
                return $"{t}: {Truncate(rest, 60)}";
            }

            // JSON Literal — collapse newlines for the tag.
            if (node.Title == "JSON Literal")
                return Truncate(raw.Replace('\n', ' ').Replace("  ", " "), 80);

            return Truncate(raw, 80);
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        // Helper: pull the raw editor text out of node.Logic regardless of
        // which marker prefix it uses (LITERAL: / CUSTOMINPUT(...) / CUSTOMLIT::).
        private static string ExtractEditorText(BareNode n)
        {
            var l = n.Logic ?? "";
            if (l.StartsWith("LITERAL:", StringComparison.Ordinal))     return l.Substring(8);
            if (l.StartsWith("CUSTOMINPUT(", StringComparison.Ordinal)) return l;
            if (l.StartsWith("CUSTOMLIT::", StringComparison.Ordinal))  return l.Substring("CUSTOMLIT::".Length);
            return "";
        }

        private static bool LiteralEditorMultiLine(string title) =>
            title == "JSON Literal" || title == "Custom Literal";

        private static string LiteralEditorHint(string title) => title switch
        {
            "JSON Literal"              => "Auto-detects: JSON shape → parsed; otherwise treated as a string.",
            "Custom Literal"            => "First line: type name. Body: JSON or plain string.",
            "Custom Input"              => "Format: CUSTOMINPUT(MyType). Becomes a method parameter.",
            "Expose"                    => "Property name to read off the connected Object.",
            "Cast"                      => "CLR type to cast Value to (e.g. \"User\", \"int\").",
            "HTTP: Read JSON"           => "CLR type to deserialise the response into.",
            "Connection String Literal" => "Postgres connection string.",
            "Predicate Literal"         => "LINQ lambda, e.g. x => x.IsActive",
            _                            => "Constant value used wherever this node is wired."
        };

        // Apply the inspector textbox to the currently-targeted node. Mirrors
        // what AttachLiteralEditor used to do inline.
        private void OnInspectorValueChanged(object sender, TextChangedEventArgs e)
        {
            if (_inspectorValueLoading || _inspectorTarget?.Data == null) return;
            var data = _inspectorTarget.Data;
            var raw = InspectorValueBox.Text ?? "";
            var title = data.Title ?? "";

            if (title == "Custom Input")
            {
                var trimmed = raw.Trim();
                var m = System.Text.RegularExpressions.Regex.Match(
                    trimmed, @"^\s*CUSTOMINPUT\s*\(\s*([\w\.]+)\s*\)\s*$");
                var typeName = m.Success ? m.Groups[1].Value : trimmed;
                data.Logic = $"CUSTOMINPUT({typeName})";
                var port = data.Outputs.FirstOrDefault();
                if (port != null && !string.IsNullOrEmpty(typeName))
                {
                    port.SemanticType   = "custom";
                    port.CustomTypeName = typeName;
                    _inspectorTarget.LoadFromBareNode(data);
                }
            }
            else if (title == "Custom Literal")
            {
                var nl = raw.IndexOf('\n');
                var typeLine = (nl < 0 ? raw : raw.Substring(0, nl)).Trim();
                var port = data.Outputs.FirstOrDefault();
                if (port != null && !string.IsNullOrEmpty(typeLine))
                {
                    port.SemanticType   = "custom";
                    port.CustomTypeName = typeLine;
                    _inspectorTarget.LoadFromBareNode(data);
                }
                data.Logic = $"CUSTOMLIT::{raw}";
            }
            else if (title == "Cast" || title == "HTTP: Read JSON")
            {
                // The inspector value here names the target CLR type. Re-type
                // the output port live so downstream connections see the new
                // CustomTypeName (codegen reads it from the port at gen time).
                var typeName = (raw ?? "").Trim();
                var port = data.Outputs.FirstOrDefault();
                if (port != null && !string.IsNullOrEmpty(typeName))
                {
                    port.SemanticType   = "custom";
                    port.CustomTypeName = typeName;
                    _inspectorTarget.LoadFromBareNode(data);
                }
                data.Logic = $"LITERAL:{typeName}";
            }
            else
            {
                data.Logic = $"LITERAL:{raw}";
            }
            // Refresh the on-canvas value tag so the user sees what they typed.
            _inspectorTarget.ValuePreview = BuildValuePreview(data);
            MarkUnsavedChanges();
        }

        private List<Category> CreateNodeLibrary() => new()
        {
            new Category { Name = "Flow", Nodes = new() {
                CreateNode("Start",       "Entry point of the function", Array.Empty<Input>(), new[]{ new Output("Flow", typeof(object), "object") }),
                CreateNode("End",         "Exit point of the function",  new[]{ new Input("Flow", typeof(object), "object", false) }, Array.Empty<Output>()),
                CreateNode("Event Input", "Graph input parameter",       Array.Empty<Input>(), new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Set Output",  "Graph output value",          new[]{ new Input("Value", typeof(object), "object", true) }, Array.Empty<Output>()),
                // Custom Input belongs with Flow — it adds a typed parameter to
                // the generated function. CUSTOMINPUT(MyType) marker in Logic.
                CreateNode("Custom Input", "Adds a custom-typed parameter to the generated function. Set Value to CUSTOMINPUT(MyType).",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "custom") }),
            }},
            new Category { Name = "Variables", Nodes = new() {
                CreateNode("Get Variable", "Retrieves a named variable",
                    new[]{ new Input("Name", typeof(string), "string", true) },
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Set Variable", "Stores a value into a named variable",
                    new[]{ new Input("Name", typeof(string), "string", true), new Input("Value", typeof(object), "object", true) },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "Math", Nodes = new() {
                CreateNode("Add",      "A + B", Num2In(), Num1Out()),
                CreateNode("Subtract", "A - B", Num2In(), Num1Out()),
                CreateNode("Multiply", "A × B", Num2In(), Num1Out()),
                CreateNode("Divide",   "A ÷ B (throws on zero)", Num2In(), Num1Out()),
            }},
            new Category { Name = "Logic", Nodes = new() {
                CreateNode("If",  "Conditional branch — accepts any value, evaluates as truthy/falsy",
                    new[]{ new Input("Condition", typeof(object), "object", true) },
                    new[]{ new Output("True", typeof(object), "object"), new Output("False", typeof(object), "object") }),
                CreateNode("And", "A && B — accepts any value (truthy semantics)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Or",  "A || B — accepts any value (truthy semantics)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Not", "!Value — accepts any value (truthy semantics)",
                    new[]{ new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                // Sequencing primitive — wire any output into After to enforce ordering.
                CreateNode("Run After", "Force this branch to run after another node finishes. Wire any output into After.",
                    new[]{ new Input("After", typeof(object), "object", true) },
                    new[]{ new Output("Then", typeof(object), "object") }),
                // Comparison operators — accept anything, compare via Equals/Comparer.
                CreateNode("Equals", "A == B (uses object.Equals)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Not Equals", "A != B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Less Than", "A < B (Comparer<object>.Default)",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Greater Than", "A > B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Less Or Equal", "A <= B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                CreateNode("Greater Or Equal", "A >= B",
                    new[]{ new Input("A", typeof(object), "object", true), new Input("B", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(bool), "bool") }),
                // Lambda wrap — Body input is treated as the lambda body string;
                // inspector field is the parameter name (default "x").
                CreateNode("Lambda", "Wrap a body expression as (param => body). Inspector field = param name.",
                    new[]{ new Input("Body", typeof(object), "object", true) },
                    new[]{ new Output("Lambda", typeof(object), "object") }),
            }},
            new Category { Name = "String", Nodes = new() {
                CreateNode("Concat", "Joins two strings",
                    new[]{ new Input("A", typeof(string), "string", true), new Input("B", typeof(string), "string", true) },
                    new[]{ new Output("Result", typeof(string), "string") }),
                CreateNode("Format", "string.Format(template, arg0)",
                    new[]{ new Input("Template", typeof(string), "string", true), new Input("Arg0", typeof(object), "object", false) },
                    new[]{ new Output("Result", typeof(string), "string") }),
            }},
            new Category { Name = "Literals", Nodes = new() {
                CreateNode("String Literal", "A constant string value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(string), "string") }),
                CreateNode("Int Literal", "A constant integer value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(int), "int") }),
                CreateNode("Float Literal", "A constant floating-point value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(double), "number") }),
                CreateNode("Bool Literal", "A constant boolean value",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(bool), "bool") }),
                CreateNode("Null", "A null reference",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("JSON Literal", "Multi-line JSON constant (parsed at runtime)",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Connection String Literal", "A Postgres connection string",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(string), "string") }),
                CreateNode("Predicate Literal", "A LINQ predicate, e.g. \"x => x.IsActive\"",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Custom Literal", "A custom-typed constant. Inspector field — first line is the type, rest is JSON or string.",
                    Array.Empty<Input>(),
                    new[]{ new Output("Value", typeof(object), "custom") }),
                CreateNode("Type Literal", "A constant Type reference (e.g., typeof(Game))",
                     Array.Empty<Input>(),
                    new[]{ new Output("Type", typeof(Type), "type") }),
             }},
            new Category { Name = "Objects", Nodes = new() {
                CreateNode("Expose", "Pull a single property out of an object. Inspector field = property name.",
                    new[]{ new Input("Object", typeof(object), "object", true) },
                    new[]{ new Output("Value", typeof(object), "object") }),
                CreateNode("Cast", "Cast a value to a target type. Inspector field = type name (e.g. \"User\").",
                    new[]{ new Input("Value", typeof(object), "object", true) },
                    new[]{ new Output("Result", typeof(object), "custom") }),
                CreateNode("WebURL", "Wraps a URL string as Uri",
                    new[]{ new Input("URL", typeof(string), "string", true) },
                    new[]{ new Output("URI", typeof(object), "weburl") }),
            }},
            new Category { Name = "HTTP", Nodes = new() {
                // All HTTP nodes default to HTTP/2 with a fallback to HTTP/1.1.
                CreateNode("HTTP: New Client",
                    "Create an HttpClient configured for HTTP/2 (HttpVersion=2.0, VersionPolicy=RequestVersionOrLower).",
                    Array.Empty<Input>(),
                    new[]{ new Output("Client", typeof(object), "custom", "HttpClient") }),
                CreateNode("HTTP: Get", "GET <url> — returns the HttpResponseMessage",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Post JSON", "POST a serialised body to <url>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true),
                        new Input("Body",   typeof(object), "object", false)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Put JSON", "PUT a serialised body to <url>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true),
                        new Input("Body",   typeof(object), "object", false)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Delete", "DELETE <url>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Url",    typeof(string), "string", true)
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Send", "Send any HttpRequestMessage (full control)",
                    new[]{
                        new Input("Client",  typeof(object), "custom", true, "HttpClient"),
                        new Input("Request", typeof(object), "custom", true, "HttpRequestMessage")
                    },
                    new[]{ new Output("Response", typeof(object), "custom", "HttpResponseMessage") }),
                CreateNode("HTTP: Read JSON", "Deserialise the response body — Inspector field = target type",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    new[]{ new Output("Value", typeof(object), "custom") }),
                CreateNode("HTTP: Read String", "Read the response body as a string",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    new[]{ new Output("Body", typeof(string), "string") }),
                CreateNode("HTTP: Status Code", "HTTP status code as an int",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    new[]{ new Output("Code", typeof(int), "int") }),
                CreateNode("HTTP: Set Bearer Token", "Authorization: Bearer <token>",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Token",  typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("HTTP: Set Header", "Add a default request header",
                    new[]{
                        new Input("Client", typeof(object), "custom", true, "HttpClient"),
                        new Input("Name",   typeof(string), "string", true),
                        new Input("Value",  typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("HTTP: Ensure Success", "Throw if the response was not 2xx",
                    new[]{ new Input("Response", typeof(object), "custom", true, "HttpResponseMessage") },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "EF Core: Easy", Nodes = new() {
                // Step-by-step beginner nodes that read like English.
                CreateNode("DB: Open", "Open the project's database (Postgres connection string in)",
                    new[]{ new Input("ConnectionString", typeof(string), "string", true) },
                    new[]{ new Output("Db", typeof(object), "custom", "AppDbContext") }),
                CreateNode("DB: Save", "Save all pending changes to disk",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Close", "Close + dispose the database",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    Array.Empty<Output>()),
                CreateNode("DB: Get All", "Get every row of one table",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                    new Input("EntityType", typeof(Type), "type", true), 
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Get One By Id", "Get a single row by its primary key",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                    new Input("EntityType", typeof(Type), "type", true),  
                        new Input("Id",         typeof(object), "object", true)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("DB: Get Where", "Get rows matching a predicate (e.g. \"x => x.IsActive\")",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Get First", "First match or null",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", true)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("DB: Count", "How many rows match",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", false)
                    },
                    new[]{ new Output("Count", typeof(int), "int") }),
                // Compound predicate nodes — build a real LINQ lambda from a
                // property name + value, no Lambda Wrap or And-as-truthy needed.
                // The output is shaped as semantic="predicate" so DB: Get First
                // / DB: Get Where / DB: Count splat it in directly.
                CreateNode("Where: Equals", "Predicate: x.<Property> == <Value>",
                    new[]{
                        new Input("Property", typeof(string), "string", true),
                        new Input("Value",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Not Equals", "Predicate: x.<Property> != <Value>",
                    new[]{
                        new Input("Property", typeof(string), "string", true),
                        new Input("Value",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Greater", "Predicate: x.<Property> > <Value>",
                    new[]{
                        new Input("Property", typeof(string), "string", true),
                        new Input("Value",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Less", "Predicate: x.<Property> < <Value>",
                    new[]{
                        new Input("Property", typeof(string), "string", true),
                        new Input("Value",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Contains", "Predicate: x.<Property>.Contains(<Value>)",
                    new[]{
                        new Input("Property", typeof(string), "string", true),
                        new Input("Value",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: And", "Combine two predicates with &&",
                    new[]{
                        new Input("A", typeof(object), "predicate", true),
                        new Input("B", typeof(object), "predicate", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),
                CreateNode("Where: Or", "Combine two predicates with ||",
                    new[]{
                        new Input("A", typeof(object), "predicate", true),
                        new Input("B", typeof(object), "predicate", true)
                    },
                    new[]{ new Output("Predicate", typeof(object), "predicate") }),

                CreateNode("DB: Add", "Stage an entity for insert (call DB: Save afterward)",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("DB: Add And Save", "Insert immediately",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Update And Save", "Persist changes on a tracked entity",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Remove And Save", "Delete a row immediately",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Entity", typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: Exists", "Does any row match?",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Predicate",  typeof(object), "object", true)
                    },
                    new[]{ new Output("Exists", typeof(bool), "bool") }),
                CreateNode("DB: Begin Tx", "Start a database transaction",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    new[]{ new Output("Tx", typeof(object), "custom", "IDbContextTransaction") }),
                CreateNode("DB: Commit Tx", "Commit a transaction",
                    new[]{ new Input("Tx", typeof(object), "custom", true, "IDbContextTransaction") },
                    Array.Empty<Output>()),
                CreateNode("DB: Rollback Tx", "Rollback a transaction",
                    new[]{ new Input("Tx", typeof(object), "custom", true, "IDbContextTransaction") },
                    Array.Empty<Output>()),
                // Result-shaping helpers
                CreateNode("DB: Order By", "Order rows ascending by a key (\"x => x.CreatedAt\")",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("KeySelector",typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Order By Desc", "Order rows descending by a key",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("KeySelector",typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Page", "Skip + Take pagination on a table",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Skip",       typeof(int),    "int",    true),
                        new Input("Take",       typeof(int),    "int",    true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("DB: Include", "Eager-load a navigation property",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("EntityType", typeof(Type), "type", true),
                        new Input("Navigation", typeof(object), "object", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                // Row-Level Security primitives
                CreateNode("DB: RLS Enable", "ALTER TABLE x ENABLE ROW LEVEL SECURITY",
                    new[]{
                        new Input("Db",    typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Disable", "ALTER TABLE x DISABLE ROW LEVEL SECURITY",
                    new[]{
                        new Input("Db",    typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Force", "Force RLS even for the table owner",
                    new[]{
                        new Input("Db",    typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Create Policy",
                    "CREATE POLICY <name> ON <table> FOR <op> TO <role> USING (<using>) WITH CHECK (<check>)",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("PolicyName", typeof(string), "string", true),
                        new Input("Operation",  typeof(string), "string", false),    // ALL / SELECT / INSERT / UPDATE / DELETE
                        new Input("Role",       typeof(string), "string", false),
                        new Input("Using",      typeof(string), "string", true),
                        new Input("WithCheck",  typeof(string), "string", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Drop Policy", "DROP POLICY IF EXISTS <name> ON <table>",
                    new[]{
                        new Input("Db",         typeof(object), "custom", true, "AppDbContext"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("PolicyName", typeof(string), "string", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("DB: RLS Set User",
                    "SET LOCAL app.current_user = '<userId>' — readable as current_setting('app.current_user') in policies",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("UserId", typeof(string), "string", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("DB: RLS Reset User", "RESET app.current_user",
                    new[]{ new Input("Db", typeof(object), "custom", true, "AppDbContext") },
                    Array.Empty<Output>()),
                CreateNode("DB: Raw SQL", "Execute a raw SQL command — escape hatch for anything",
                    new[]{
                        new Input("Db",     typeof(object), "custom", true, "AppDbContext"),
                        new Input("Sql",    typeof(string), "string", true),
                        new Input("Params", typeof(object), "object", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
            }},
            new Category { Name = "Postgres: Easy", Nodes = new() {
                CreateNode("PG: Connect", "Open a Postgres connection (Npgsql)",
                    new[]{ new Input("ConnectionString", typeof(string), "string", true) },
                    new[]{ new Output("Connection", typeof(object), "custom", "NpgsqlConnection") }),
                CreateNode("PG: Query", "SELECT — returns rows (Dapper)",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true),
                        new Input("Params",     typeof(object), "object", false)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("PG: Query First", "First row only — null if none",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true),
                        new Input("Params",     typeof(object), "object", false)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("PG: Execute", "INSERT/UPDATE/DELETE/DDL — returns rows affected",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true),
                        new Input("Params",     typeof(object), "object", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Insert", "INSERT INTO <table> VALUES (@…) RETURNING id",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Entity",     typeof(object), "custom", true)
                    },
                    new[]{ new Output("Id", typeof(object), "object") }),
                CreateNode("PG: Update By Id", "UPDATE <table> SET … WHERE id = @id",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Entity",     typeof(object), "custom", true),
                        new Input("Id",         typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Delete By Id", "DELETE FROM <table> WHERE id = @id",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Id",         typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Close", "Close + dispose a connection",
                    new[]{ new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection") },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "Postgres: Advanced", Nodes = new() {
                CreateNode("PG: Bulk Insert", "Streaming COPY — fastest for large inserts. Rows = IEnumerable<T>",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Table",      typeof(string), "string", true),
                        new Input("Columns",    typeof(object), "object", true), // string[]
                        new Input("Rows",       typeof(object), "object", true)
                    },
                    new[]{ new Output("Affected", typeof(long), "long") }),
                CreateNode("PG: Begin Tx", "Open a transaction",
                    new[]{ new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection") },
                    new[]{ new Output("Transaction", typeof(object), "custom", "NpgsqlTransaction") }),
                CreateNode("PG: Commit Tx", "Commit a transaction",
                    new[]{ new Input("Transaction", typeof(object), "custom", true, "NpgsqlTransaction") },
                    Array.Empty<Output>()),
                CreateNode("PG: Rollback Tx", "Rollback a transaction",
                    new[]{ new Input("Transaction", typeof(object), "custom", true, "NpgsqlTransaction") },
                    Array.Empty<Output>()),
                CreateNode("PG: Prepare", "Pre-compile a SQL statement; reuse via PG: Run Prepared",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Sql",        typeof(string), "string", true)
                    },
                    new[]{ new Output("Command", typeof(object), "custom", "NpgsqlCommand") }),
                CreateNode("PG: Run Prepared", "Execute a prepared command with named params",
                    new[]{
                        new Input("Command", typeof(object), "custom", true, "NpgsqlCommand"),
                        new Input("Params",  typeof(object), "object", false)
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Batch Execute", "Send a batch of statements in one round-trip (NpgsqlBatch)",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Statements", typeof(object), "object", true) // string[]
                    },
                    new[]{ new Output("Affected", typeof(int), "int") }),
                CreateNode("PG: Notify", "NOTIFY <channel>, <payload>",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Channel",    typeof(string), "string", true),
                        new Input("Payload",    typeof(string), "string", false)
                    },
                    Array.Empty<Output>()),
                CreateNode("PG: Listen", "LISTEN <channel> + register a callback",
                    new[]{
                        new Input("Connection", typeof(object), "custom", true, "NpgsqlConnection"),
                        new Input("Channel",    typeof(string), "string", true),
                        new Input("Callback",   typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
            }},
            new Category { Name = "SpacetimeDB: Easy", Nodes = new() {
                CreateNode("SDB: Connect", "Open a SpacetimeDB client connection",
                    new[]{
                        new Input("Uri",      typeof(string), "string", true),
                        new Input("Module",   typeof(string), "string", true),
                        new Input("AuthToken",typeof(string), "string", false)
                    },
                    new[]{ new Output("Conn", typeof(object), "custom", "DbConnection") }),
                CreateNode("SDB: Disconnect", "Close the client connection",
                    new[]{ new Input("Conn", typeof(object), "custom", true, "DbConnection") },
                    Array.Empty<Output>()),
                CreateNode("SDB: Subscribe", "Subscribe to one or more SQL queries",
                    new[]{
                        new Input("Conn",    typeof(object), "custom", true, "DbConnection"),
                        new Input("Queries", typeof(object), "object", true) // string[]
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: Call Reducer", "Invoke a server-side reducer by name with positional args",
                    new[]{
                        new Input("Conn",    typeof(object), "custom", true, "DbConnection"),
                        new Input("Reducer", typeof(string), "string", true),
                        new Input("Args",    typeof(object), "object", false)  // params object[]
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: Iter Table", "Iterate a synced table",
                    new[]{
                        new Input("Conn",  typeof(object), "custom", true, "DbConnection"),
                        new Input("Table", typeof(string), "string", true)
                    },
                    new[]{ new Output("Rows", typeof(object), "object") }),
                CreateNode("SDB: Find By Pk", "Find a row by primary key on a synced table",
                    new[]{
                        new Input("Conn",  typeof(object), "custom", true, "DbConnection"),
                        new Input("Table", typeof(string), "string", true),
                        new Input("Pk",    typeof(object), "object", true)
                    },
                    new[]{ new Output("Row", typeof(object), "object") }),
                CreateNode("SDB: On Insert", "Register an insert callback for a table",
                    new[]{
                        new Input("Conn",     typeof(object), "custom", true, "DbConnection"),
                        new Input("Table",    typeof(string), "string", true),
                        new Input("Callback", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: On Update", "Register an update callback for a table",
                    new[]{
                        new Input("Conn",     typeof(object), "custom", true, "DbConnection"),
                        new Input("Table",    typeof(string), "string", true),
                        new Input("Callback", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
                CreateNode("SDB: On Delete", "Register a delete callback for a table",
                    new[]{
                        new Input("Conn",     typeof(object), "custom", true, "DbConnection"),
                        new Input("Table",    typeof(string), "string", true),
                        new Input("Callback", typeof(object), "object", true)
                    },
                    Array.Empty<Output>()),
            }}
        };

        private static Input[] Num2In() => new[] { new Input("A", typeof(double), "number", true), new Input("B", typeof(double), "number", true) };
        private static Output[] Num1Out()    => new[] { new Output("Result", typeof(double), "number") };
        private static Input[] Bool2In() => new[] { new Input("A", typeof(bool), "bool", true), new Input("B", typeof(bool), "bool", true) };
        private static Output[] Bool1Out()    => new[] { new Output("Result", typeof(bool), "bool") };

        private List<BareNode> _customScripts = new();

        private void SetupSidebar()
        {
            UIScroll.Content = null;
            var sidebar = new StackPanel();

            // CREATE CUSTOM NODE input panel
            sidebar.Children.Add(CreateScriptPanel());

            // CUSTOM NODES list
            var section = new StackPanel { Margin = new Thickness(8, 0, 8, 8) };

            if (_customScripts.Any())
            {
                section.Children.Add(new TextBlock
                {
                    Text = "CUSTOM NODES",
                    FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                    FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go"),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                foreach (var s in _customScripts)
                    AddCustomScriptItem(section, s);
            }
            else
            {
                section.Children.Add(new TextBlock
                {
                    Text = "Type a C# signature above and click Add Node.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go"),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            sidebar.Children.Add(section);
            UIScroll.Content = sidebar;
        }
        private FrameworkElement CreateScriptPanel()
        {
            var panel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(8),
                Padding = new Thickness(12)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "CREATE CUSTOM NODE",
                FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var scriptInput = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 55,
                FontSize = 11, FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(152, 195, 121)),
                Padding = new Thickness(8, 6, 8, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68))
            };

            // Watermark
            var watermark = new TextBlock
            {
                Text = "public string MyMethod(int a, string b)",
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90)),
                IsHitTestVisible = false,
                FontSize = 11, FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(9, 8, 0, 0)
            };
            var inputGrid = new Grid();
            inputGrid.Children.Add(scriptInput);
            inputGrid.Children.Add(watermark);
            scriptInput.TextChanged += (s, e) =>
                watermark.Visibility = string.IsNullOrEmpty(scriptInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            stack.Children.Add(inputGrid);

            var addBtn = new Button
            {
                Content = "Add Node from Script",
                Margin = new Thickness(0, 8, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                Foreground = Brushes.White,
                FontSize = 12, Padding = new Thickness(8, 6, 8, 6),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go")
            };
            addBtn.Click += (s, e) =>
            {
                var node = ParseScript(scriptInput.Text.Trim());
                if (node == null) return;
                _customScripts.Add(node);
                _lastSidebarAddPosition = new Point(_lastSidebarAddPosition.X + 30, _lastSidebarAddPosition.Y + 30);
                AddNodeFromTemplateAt(node, _lastSidebarAddPosition);
                SetupSidebar();
                scriptInput.Text = "";
            };
            stack.Children.Add(addBtn);
            panel.Child = stack;
            return panel;
        }

        private void AddCustomScriptItem(StackPanel parent, BareNode node)
        {
            var item = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 5), Padding = new Thickness(10, 7, 10, 7),
                Cursor = Cursors.Hand
            };
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = node.Title, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0,0,0,4) });
            foreach (var inp in node.Inputs)
                content.Children.Add(new TextBlock
                {
                    Text = $"  ↳ {inp.Name}: {inp.SemanticType ?? "object"}{(inp.Required ? " *" : "")}",
                    FontSize = 10, FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(130, 200, 130))
                });
            foreach (var outp in node.Outputs)
                content.Children.Add(new TextBlock
                {
                    Text = $"  ⇒ {outp.Name}: {outp.SemanticType ?? "object"}",
                    FontSize = 10, FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 80))
                });
            item.Child = content;

item.MouseLeftButtonDown += (s, e) => { AddNodeFromTemplate(node); e.Handled = true; };
            item.MouseRightButtonDown += (s, e) =>
            {
                e.Handled = true; // prevent workspace context menu from opening
                var menu = new ContextMenu
                {
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                    BorderThickness = new Thickness(1),
                    PlacementTarget = item,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
                };
                var edit = new MenuItem { Header = "Edit" };
                edit.Click += (s2, e2) => ShowEditCustomScriptDialog(node);
                var del = new MenuItem { Header = "Delete" };
                del.Click += (s2, e2) => { _customScripts.Remove(node); SetupSidebar(); };
                menu.Items.Add(edit);
                menu.Items.Add(del);
                item.ContextMenu = menu;
                menu.IsOpen = true;
            };
            parent.Children.Add(item);
        }

        private void ShowEditCustomScriptDialog(BareNode node)
        {
            var titleBox = new TextBox
            {
                Text = node.Title,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var logicBox = new TextBox
            {
                Text = node.Logic ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 80,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas")
            };

            ShowMiniDialog("Edit Custom Node", dialog =>
            {
                dialog.Children.Add(new TextBlock { Text = "Title:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) });
                dialog.Children.Add(titleBox);
                dialog.Children.Add(new TextBlock { Text = "Logic:", Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 4) });
                dialog.Children.Add(logicBox);

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 15, 0, 0) };
                var saveBtn = new Button { Content = "Save", Width = 80, Background = new SolidColorBrush(Color.FromRgb(255, 76, 76)), Foreground = Brushes.White };
                saveBtn.Click += (s, e) =>
                {
                    node.Title = titleBox.Text;
                    node.Logic = logicBox.Text;
                    var idx = _customScripts.IndexOf(node);
                    if (idx >= 0) _customScripts[idx] = node;
                    SetupSidebar();
                    CloseAllOverlays();
                };
                var cancelBtn = new Button { Content = "Cancel", Width = 80, Background = new SolidColorBrush(Color.FromRgb(68, 68, 68)), Foreground = Brushes.White };
                cancelBtn.Click += (s, e) => CloseAllOverlays();
                btnRow.Children.Add(saveBtn);
                btnRow.Children.Add(cancelBtn);
                dialog.Children.Add(btnRow);
            }, Color.FromRgb(255, 76, 76), closeOnAction: false);
        }

        private void CloseAllOverlays()
        {
            var main = this.Content as Grid;
            if (main == null) return;
            var toRemove = new List<UIElement>();
            foreach (UIElement child in main.Children)
            {
                if (child is Grid g && g.Background != null && g.Background is SolidColorBrush sb && sb.Color.A == 180)
                    toRemove.Add(g);
            }
            foreach (var el in toRemove) main.Children.Remove(el);
        }

        private void AddScriptNode()
        {
            // handled inline in CreateScriptPanel now
        }

        private void SetupWorkspaceContextMenu()
        {
            _workspaceContextMenu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go"),
                FontSize = 14,
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                BorderThickness = new Thickness(1)
            };

            AddMenuItem("Add Node...",    () => ShowAddNodePopup(_lastContextMenuPosition));
            AddMenuItem("Add Note",       () => AddNewNote());
            AddMenuItem("Add Group",       () => AddNewChunk());
            _workspaceContextMenu.Items.Add(new Separator());
            AddMenuItem("Copy",           () => CopySelectedNodes());
            AddMenuItem("Paste",          () => PasteNodes());
            AddMenuItem("Delete",         () => DeleteSelectedNodes());
            _workspaceContextMenu.Items.Add(new Separator());
            AddMenuItem("Select All",     () => SelectAllNodes());
            AddMenuItem("Check Warnings", () => { CheckAndShowWarnings(); RefreshAll(); });
            AddMenuItem("Manage Usings…", () => ShowUsingsDialog());
            AddMenuItem("Import Entity…", () => ShowEntityImportDialog());
        }

        /// <summary>
        /// Edit the project-level using directives. Each line in the textbox
        /// is one namespace (with or without a leading "using"/trailing ";").
        /// Saved to CurrentSession.Usings; the compiler unions these with its
        /// defaults and any "using …;" lines hoisted out of custom-node Logic
        /// before emitting them at the top of the generated file.
        /// </summary>
        private void ShowUsingsDialog()
        {
            CurrentSession.Usings ??= new List<string>();
            var initial = string.Join("\n", CurrentSession.Usings);

            var box = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping  = TextWrapping.NoWrap,
                Height        = 220,
                Width         = 420,
                FontFamily    = new FontFamily("Consolas"),
                FontSize      = 12,
                Background    = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground    = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Padding       = new Thickness(8, 6, 8, 6),
                BorderBrush   = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                BorderThickness = new Thickness(1),
                Text          = initial
            };

            ShowMiniDialog("Project Usings", dialog =>
            {
                dialog.Children.Add(new TextBlock
                {
                    Text = "One namespace per line. \"using\" / \";\" are optional.\n" +
                           "These are merged with the compiler's defaults and any\n" +
                           "using-lines lifted out of custom-node Logic.",
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                dialog.Children.Add(box);
                AddButtons(dialog, Color.FromRgb(255, 76, 76), "Save", () =>
                {
                    var lines = (box.Text ?? "")
                        .Split('\n')
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .Select(l => l.StartsWith("using ", StringComparison.Ordinal) ? l.Substring(6) : l)
                        .Select(l => l.TrimEnd(';').Trim())
                        .Where(l => l.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    CurrentSession.Usings = lines;
                    MarkUnsavedChanges();
                });
            });
        }

        private void AddMenuItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            _workspaceContextMenu.Items.Add(item);
        }

        private void WorkspaceCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Preserve native clipboard menu on text edits
            if (e.OriginalSource is TextBox) return;

            // Walk up looking for a NodeControl. Earlier code returned silently
            // when right-clicking inside a node, which left users with no menu
            // at all (nodes didn't have their own right-click handler). Now we
            // route the click to the workspace context menu regardless — but
            // remember the node so future per-node menus can hook in.
            NodeControl hitNode = null;
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is NodeControl nc) { hitNode = nc; break; }
                dep = VisualTreeHelper.GetParent(dep);
            }

            var canvasPos = e.GetPosition(WorkspaceCanvas);
            _lastContextMenuPosition = canvasPos;

            // If the click landed on a node, make sure it's selected so any
            // node-specific actions in the workspace menu act on the right one.
            if (hitNode != null && !_selectedNodes.Contains(hitNode))
            {
                DeselectAllNodes();
                SelectNode(hitNode);
                ShowNodeInspector(hitNode);
            }

            // ChunksCanvas sits below the transparent WorkspaceCanvas in z-order,
            // so a right-click on a chunk hits WorkspaceCanvas instead of the chunk.
            // Hit-test manually against chunk bounds and route to the chunk menu.
            foreach (UIElement child in ChunksCanvas.Children)
            {
                if (child is ChunkControl chunk)
                {
                    double cl = Canvas.GetLeft(chunk); double ct = Canvas.GetTop(chunk);
                    if (double.IsNaN(cl)) cl = 0;
                    if (double.IsNaN(ct)) ct = 0;
                    double cw = chunk.ActualWidth > 0 ? chunk.ActualWidth : chunk.Width;
                    double ch = chunk.ActualHeight > 0 ? chunk.ActualHeight : chunk.Height;
                    if (canvasPos.X >= cl && canvasPos.X <= cl + cw &&
                        canvasPos.Y >= ct && canvasPos.Y <= ct + ch)
                    {
                        ShowChunkContextMenu(chunk);
                        e.Handled = true;
                        return;
                    }
                }
            }

            _workspaceContextMenu.PlacementTarget = WorkspaceCanvas;
            _workspaceContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            _workspaceContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void RefreshAll()
        {
            RefreshConnections();
            CheckAndShowWarnings();
        }

        private void DeleteSelectedNode_Click2(object sender, RoutedEventArgs e)
        {
            if (_selectedNodes.Count > 0) { DeleteSelectedNodes(); HideNodeInspector(); }
        }

        private void SetupGridPanning()
        {
            WorkspaceCanvas.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    _isPanning = true;
                    _lastPanPosition = e.GetPosition(this);
                    WorkspaceCanvas.CaptureMouse();
                }
            };

            WorkspaceCanvas.MouseMove += (s, e) =>
            {
                if (_isPanning)
                {
                    var cur = e.GetPosition(this);
                    var delta = cur - _lastPanPosition;

                    void PanLayer(Canvas cv)
                    {
                        foreach (UIElement child in cv.Children)
                        {
                            if (child == _selectionBox) continue;
                            var l = Canvas.GetLeft(child); var t = Canvas.GetTop(child);
                            Canvas.SetLeft(child, (double.IsNaN(l) ? 0 : l) + delta.X);
                            Canvas.SetTop(child,  (double.IsNaN(t) ? 0 : t) + delta.Y);
                        }
                    }
                    PanLayer(WorkspaceCanvas);
                    PanLayer(ChunksCanvas);
                    PanLayer(NotesCanvas);

                    _panOffsetX += delta.X;
                    _panOffsetY += delta.Y;

                    _lastPanPosition = cur;
                    Dispatcher.BeginInvoke(new Action(RefreshConnections),
                        System.Windows.Threading.DispatcherPriority.Render);
                }
            };

            WorkspaceCanvas.MouseUp += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle || !WorkspaceCanvas.IsMouseCaptured)
                {
                    _isPanning = false;
                    if (WorkspaceCanvas.IsMouseCaptured) WorkspaceCanvas.ReleaseMouseCapture();
                }
            };
        }

        private void SetupSelectionBox()
        {
            _selectionBox = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(76, 132, 255)),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(50, 76, 132, 255)),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            WorkspaceCanvas.Children.Add(_selectionBox);

            WorkspaceCanvas.MouseLeftButtonDown += (s, e) =>
            {
                if (e.Source != WorkspaceCanvas) return;

                // ChunksCanvas sits below WorkspaceCanvas, so plain left-clicks
                // on a chunk land here instead of on the chunk. Hit-test
                // manually and route to the chunk's drag/select machinery —
                // mirrors what the right-click handler already does. Also
                // route resize-thumb hits so the bottom-right gripper works.
                var canvasPos = e.GetPosition(WorkspaceCanvas);
                foreach (var chunk in ChunksCanvas.Children.OfType<ChunkControl>())
                {
                    double cl = Canvas.GetLeft(chunk); if (double.IsNaN(cl)) cl = 0;
                    double ct = Canvas.GetTop(chunk);  if (double.IsNaN(ct)) ct = 0;
                    double cw = chunk.ActualWidth  > 0 ? chunk.ActualWidth  : chunk.Width;
                    double ch = chunk.ActualHeight > 0 ? chunk.ActualHeight : chunk.Height;
                    if (canvasPos.X < cl || canvasPos.X > cl + cw) continue;
                    if (canvasPos.Y < ct || canvasPos.Y > ct + ch) continue;

                    // Bottom-right 24×24 → resize start.
                    bool inResize = canvasPos.X >= cl + cw - 28 && canvasPos.Y >= ct + ch - 28;
                    chunk.BeginPointerInteraction(canvasPos, inResize);
                    e.Handled = true;
                    return;
                }

                DeselectAll();
                _isSelecting = true;
                _selectionStart = canvasPos;
                Canvas.SetLeft(_selectionBox, _selectionStart.X);
                Canvas.SetTop(_selectionBox, _selectionStart.Y);
                _selectionBox.Width = _selectionBox.Height = 0;
                _selectionBox.Visibility = Visibility.Visible;
                WorkspaceCanvas.CaptureMouse();
                e.Handled = true;
            };

            WorkspaceCanvas.MouseMove += (s, e) =>
            {
                // Forward to any chunk currently in drag/resize.
                var cur = e.GetPosition(WorkspaceCanvas);
                foreach (var chunk in ChunksCanvas.Children.OfType<ChunkControl>())
                {
                    if (chunk.IsExternallyInteracting)
                    {
                        chunk.UpdatePointerInteraction(cur);
                        Dispatcher.BeginInvoke(new Action(RefreshConnections),
                            System.Windows.Threading.DispatcherPriority.Render);
                        return;
                    }
                }

                if (!_isSelecting) return;
                Canvas.SetLeft(_selectionBox, Math.Min(_selectionStart.X, cur.X));
                Canvas.SetTop(_selectionBox,  Math.Min(_selectionStart.Y, cur.Y));
                _selectionBox.Width  = Math.Abs(cur.X - _selectionStart.X);
                _selectionBox.Height = Math.Abs(cur.Y - _selectionStart.Y);
            };

            WorkspaceCanvas.MouseLeftButtonUp += (s, e) =>
            {
                bool endedChunkInteraction = false;
                foreach (var chunk in ChunksCanvas.Children.OfType<ChunkControl>())
                {
                    if (chunk.IsExternallyInteracting)
                    {
                        chunk.EndPointerInteraction();
                        endedChunkInteraction = true;
                    }
                }
                if (endedChunkInteraction) { MarkUnsavedChanges(); return; }

                if (!_isSelecting) return;
                _isSelecting = false;
                _selectionBox.Visibility = Visibility.Collapsed;
                WorkspaceCanvas.ReleaseMouseCapture();

                var selRect = new Rect(
                    Canvas.GetLeft(_selectionBox), Canvas.GetTop(_selectionBox),
                    _selectionBox.Width, _selectionBox.Height);

                foreach (var nc in WorkspaceCanvas.Children.OfType<NodeControl>())
                {
                    var l = Canvas.GetLeft(nc); var t = Canvas.GetTop(nc);
                    if (selRect.IntersectsWith(new Rect(double.IsNaN(l) ? 0 : l, double.IsNaN(t) ? 0 : t, nc.ActualWidth, nc.ActualHeight)))
                        SelectNode(nc);
                }
                e.Handled = true;
            };
        }

        private void SelectNode(NodeControl node)
        {
            if (_selectedNodes.Contains(node)) return;
            _selectedNodes.Add(node);
            node.NodeBorderBrush = new SolidColorBrush(Color.FromRgb(76, 132, 255));
            node.NodeBorderThickness = new Thickness(2);
        }

        private void DeselectAllNodes()
        {
            foreach (var n in _selectedNodes)
            {
                n.NodeBorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
                n.NodeBorderThickness = new Thickness(3);
            }
            _selectedNodes.Clear();
            HideNodeInspector();
        }

        private void DeselectAllConnections()
        {
            _selectedConnection?.Deselect();
            _selectedConnection = null;
        }

        private void DeselectAll()
        {
            DeselectAllNodes();
            DeselectAllConnections();
            foreach (var n in _selectedNotes) n.Selected = false;
            foreach (var c in _selectedChunks) c.Selected = false;
            _selectedNotes.Clear();
            _selectedChunks.Clear();
        }

        private void SelectAllNodes()
        {
            foreach (var nc in WorkspaceCanvas.Children.OfType<NodeControl>()) SelectNode(nc);
        }

        private void DeleteSelectedNodes()
        {
            foreach (var nc in _selectedNodes.ToList())
            {
                var toRemove = CurrentSession.Connections
                    .Where(c => c.Node1UUID == nc.Data.UUID || c.Node2UUID == nc.Data.UUID).ToList();
                foreach (var c in toRemove) CurrentSession.Connections.Remove(c);
                CurrentSession.Nodes.Remove(nc.Data);
                CurrentSession.NodePositions.Remove(nc.Data.UUID);
                WorkspaceCanvas.Children.Remove(nc);
            }
            _selectedNodes.Clear();

            foreach (var n in _selectedNotes.ToList()) NotesCanvas.Children.Remove(n);
            foreach (var c in _selectedChunks.ToList()) ChunksCanvas.Children.Remove(c);
            _selectedNotes.Clear(); _selectedChunks.Clear();

            RefreshConnections();
            MarkUnsavedChanges();
        }

        private void SetupKeyboardShortcuts()
        {
            KeyDown += (s, e) =>
            {
                bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                if (ctrl && e.Key == Key.A) SelectAllNodes();
                if (ctrl && e.Key == Key.C) CopySelectedNodes();
                if (ctrl && e.Key == Key.V) PasteNodes();
                if (ctrl && e.Key == Key.D) { CopySelectedNodes(); PasteNodes(); }
                if (ctrl && e.Key == Key.S) _ = SaveSession();
                if (e.Key == Key.Delete)
                {
                    if (_selectedConnection != null) DeleteConnection(_selectedConnection.Connection);
                    else if (_selectedNodes.Any() || _selectedNotes.Any() || _selectedChunks.Any()) DeleteSelectedNodes();
                }
            };
        }

        private Point _copyOrigin = new(double.NaN, double.NaN);
        private Dictionary<NodeControl, BareNode> _copiedNodes = new();

        private void CopySelectedNodes()
        {
            _copiedNodes.Clear();
            _copyOrigin = new(double.NaN, double.NaN);
            if (!_selectedNodes.Any()) return;
            _copyOrigin = new(_selectedNodes.Min(n => Canvas.GetLeft(n)), _selectedNodes.Min(n => Canvas.GetTop(n)));
            foreach (var nc in _selectedNodes) _copiedNodes[nc] = nc.Data;
        }

        private void PasteNodes()
        {
            if (double.IsNaN(_copyOrigin.X)) return;
            var offset = new Point(50, 50);
            foreach (var kvp in _copiedNodes)
            {
                var newNode = CloneNode(kvp.Value);
                CurrentSession.Nodes.Add(newNode);
                var vc = new NodeControl();
                vc.LoadFromBareNode(newNode);
                SetupNodeEvents(vc);
                WorkspaceCanvas.Children.Add(vc);
                Canvas.SetLeft(vc, Canvas.GetLeft(kvp.Key) + offset.X);
                Canvas.SetTop(vc,  Canvas.GetTop(kvp.Key)  + offset.Y);
                SelectNode(vc);
            }
            MarkUnsavedChanges();
        }

        private void AddNodeFromTemplate(BareNode templateNode)
        {
            _lastSidebarAddPosition = new(_lastSidebarAddPosition.X + 30, _lastSidebarAddPosition.Y + 30);
            AddNodeFromTemplateAt(templateNode, _lastSidebarAddPosition);
        }

        private void AddNodeFromTemplateAt(BareNode templateNode, Point position)
        {
            var newNode = CloneNode(templateNode);
            CurrentSession.Nodes.Add(newNode);

            var vc = new NodeControl();
            vc.LoadFromBareNode(newNode);
            SetupNodeEvents(vc);
            WorkspaceCanvas.Children.Add(vc);
            Canvas.SetLeft(vc, position.X);
            Canvas.SetTop(vc,  position.Y);
            MarkUnsavedChanges();
            CheckAndShowWarnings();

            // Literal editing happens in the right-side Node Inspector now.
            vc.ValuePreview = BuildValuePreview(newNode);

            // Defer connection refresh until layout so port positions are accurate
            vc.Loaded += (s2, e2) =>
                Dispatcher.BeginInvoke(new Action(RefreshConnections),
                    System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void AttachLiteralEditor(NodeControl vc, BareNode node, string placeholder)
        {
            // Walk to the first StackPanel with 2+ children (the main node layout panel)
            DependencyObject cur = vc;
            StackPanel host = null;
            for (int depth = 0; depth < 10 && host == null; depth++)
            {
                int cnt = System.Windows.Media.VisualTreeHelper.GetChildrenCount(cur);
                for (int j = 0; j < cnt && host == null; j++)
                {
                    var ch = System.Windows.Media.VisualTreeHelper.GetChild(cur, j);
                    if (ch is StackPanel sp && sp.Children.Count >= 2) host = sp;
                }
                if (cnt > 0) cur = System.Windows.Media.VisualTreeHelper.GetChild(cur, 0);
                else break;
            }
            if (host == null) return;

            // Decide editor shape based on the node title.
            //   JSON Literal → multi-line, monospace, ~140 px tall
            //   Custom Input → store CUSTOMINPUT(Type) and re-type the output port live
            //   Predicate Literal → wider single-line (lambda code)
            //   everything else → single-line
            string title = node.Title ?? "";
            bool multiLine     = title == "JSON Literal" || title == "Custom Literal";
            bool isCustomInput = title == "Custom Input";
            bool isCustomLit   = title == "Custom Literal";

            string LogicToText(string logic)
            {
                if (string.IsNullOrEmpty(logic)) return "";
                if (logic.StartsWith("LITERAL:", StringComparison.Ordinal))     return logic.Substring(8);
                if (logic.StartsWith("CUSTOMINPUT(", StringComparison.Ordinal)) return logic;
                if (logic.StartsWith("CUSTOMLIT::", StringComparison.Ordinal))  return logic.Substring("CUSTOMLIT::".Length);
                return "";
            }

            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            var tb = new TextBox
            {
                Text = LogicToText(node.Logic),
                AcceptsReturn   = multiLine,
                TextWrapping    = multiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Height          = multiLine ? 140 : double.NaN,
                Background      = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                Foreground      = new SolidColorBrush(Color.FromRgb(152, 195, 121)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(6, 3, 6, 3),
                FontFamily      = new FontFamily("Consolas"),
                FontSize        = 12,
                MinWidth        = title == "Predicate Literal" ? 180 : 90,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalScrollBarVisibility = multiLine ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };
            var wm = new TextBlock
            {
                Text             = placeholder,
                Foreground       = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                IsHitTestVisible = false,
                FontFamily       = new FontFamily("Consolas"),
                FontSize         = 12,
                Margin           = new Thickness(8, 4, 0, 0),
                Visibility       = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed
            };
            tb.TextChanged += (s, e) =>
            {
                wm.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;

                if (isCustomInput)
                {
                    var raw = (tb.Text ?? "").Trim();
                    var m = System.Text.RegularExpressions.Regex.Match(
                        raw, @"^\s*CUSTOMINPUT\s*\(\s*([\w\.]+)\s*\)\s*$");
                    var typeName = m.Success ? m.Groups[1].Value : raw;
                    node.Logic = $"CUSTOMINPUT({typeName})";

                    var port = node.Outputs.FirstOrDefault();
                    if (port != null)
                    {
                        port.SemanticType   = "custom";
                        port.CustomTypeName = typeName;
                        vc.LoadFromBareNode(node);
                    }
                }
                else if (isCustomLit)
                {
                    // First line is the type name, rest is JSON.
                    var raw = tb.Text ?? "";
                    var nl = raw.IndexOf('\n');
                    var typeLine = (nl < 0 ? raw : raw.Substring(0, nl)).Trim();
                    // Re-type the output port to match.
                    var port = node.Outputs.FirstOrDefault();
                    if (port != null && !string.IsNullOrEmpty(typeLine))
                    {
                        port.SemanticType   = "custom";
                        port.CustomTypeName = typeLine;
                        vc.LoadFromBareNode(node);
                    }
                    node.Logic = $"CUSTOMLIT::{raw}";
                }
                else
                {
                    node.Logic = $"LITERAL:{tb.Text}";
                }
            };
            tb.MouseLeftButtonDown += (s, e) => e.Handled = true; // prevent drag
            grid.Children.Add(tb);
            grid.Children.Add(wm);
            host.Children.Insert(1, grid);
        }

        // Whether a node should get the inline value editor.
        private static bool NeedsLiteralEditor(BareNode n) =>
               n.Title != null
            && (n.Title.EndsWith("Literal", StringComparison.Ordinal)
                || n.Title == "Null"
                || n.Title == "Custom Input"
                || n.Title == "Custom Literal"
                || n.Title == "Expose"
                || n.Title == "Cast"
                || n.Title == "HTTP: Read JSON"
                || n.Title == "Lambda");

        private static string LiteralPlaceholderFor(string title) => title switch
        {
            "String Literal"            => "hello",
            "Int Literal"               => "42",
            "Float Literal"             => "3.14f",
            "Bool Literal"              => "true",
            "Null"                      => "(null)",
            "JSON Literal"              => "{\n  \"key\": \"value\"\n}",
            "Connection String Literal" => "Host=…;Username=…;Password=…;Database=…",
            "Predicate Literal"         => "x => x.IsActive",
            "Custom Input"              => "CUSTOMINPUT(MyType)",
            "Custom Literal"            => "MyType\n{\n  \"name\": \"value\"\n}",
            "Expose"                    => "PropertyName",
            "Cast"                      => "TargetType",
            "HTTP: Read JSON"           => "TargetType",
            "Lambda"                    => "x",
            _                            => "value"
        };

        private static BareNode CloneNode(BareNode src) => new()
        {
            Title = src.Title,
            Description = src.Description,
            Inputs  = new HashSet<Input>(src.Inputs.Select(i => new Input(i.Name, i.Type, i.SemanticType, i.Required, i.CustomTypeName))),
            Outputs = new HashSet<Output>(src.Outputs.Select(o => new Output(o.Name, o.Type, o.SemanticType, o.CustomTypeName))),
            UUID = Guid.NewGuid().ToString(),
            Logic = src.Logic,
            SyncType = src.SyncType
        };

        private static BareNode CreateNode(string title, string desc,
            IEnumerable<Input> inputs, IEnumerable<Output> outputs, string logic = null) => new()
        {
            Title = title, Description = desc,
            Inputs = inputs.ToHashSet(), Outputs = outputs.ToHashSet(),
            UUID = Guid.NewGuid().ToString(),
            Logic = logic ?? $"// {title}",
            SyncType = "Sync"
        };

        private void SetupNodeEvents(NodeControl vc)
        {
            vc.OnNodeClicked += node =>
            {
                if (_isUpdatingSelection) return;
                _isUpdatingSelection = true;
                try
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        if (_selectedNodes.Contains(node))
                        {
                            _selectedNodes.Remove(node);
                            node.NodeBorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
                            node.NodeBorderThickness = new Thickness(3);
                            if (_selectedNodes.Count == 1) ShowNodeInspector(_selectedNodes[0]);
                            else if (!_selectedNodes.Any()) HideNodeInspector();
                        }
                        else SelectNode(node);
                    }
                    else
                    {
                        if (!_selectedNodes.Contains(node)) { DeselectAllNodes(); SelectNode(node); }
                        ShowNodeInspector(node);
                    }
                }
                finally { _isUpdatingSelection = false; }
            };

            vc.OnDragStarted += () => vc.BringToFront();

            vc.OnDragDelta += delta =>
            {
                foreach (var sel in _selectedNodes)
                {
                    if (sel == vc) continue;
                    var l = Canvas.GetLeft(sel); var t = Canvas.GetTop(sel);
                    Canvas.SetLeft(sel, (double.IsNaN(l) ? 0 : l) + delta.X);
                    Canvas.SetTop(sel,  (double.IsNaN(t) ? 0 : t) + delta.Y);
                }
                Dispatcher.BeginInvoke(new Action(RefreshConnections), System.Windows.Threading.DispatcherPriority.Render);
                MarkUnsavedChanges();
            };

            // One final accurate refresh when the mouse button lifts
            vc.MouseLeftButtonUp += (s, e) =>
                Dispatcher.BeginInvoke(new Action(RefreshConnections),
                    System.Windows.Threading.DispatcherPriority.Render);

            vc.OnPortMouseDown += (s, e) =>
            {
                _isDraggingConnection = true;
                _dragStartNode = vc;
                _dragStartPort = e.PortName;
                _dragStartIsOutput = e.IsOutput;

                _dragLine = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                    StrokeThickness = 3,
                    X1 = e.Position.X, Y1 = e.Position.Y,
                    X2 = e.Position.X, Y2 = e.Position.Y,
                    IsHitTestVisible = false,
                    StrokeDashArray = new DoubleCollection { 4, 3 }
                };
                ConnectionsCanvas.Children.Add(_dragLine);
            };

            vc.OnPortMouseMove += (s, e) =>
            {
                if (_isDraggingConnection && _dragLine != null)
                { _dragLine.X2 = e.Position.X; _dragLine.Y2 = e.Position.Y; }
            };

            vc.OnPortMouseUp += (s, e) =>
            {
                if (!_isDraggingConnection) return;
                bool sameNode = _dragStartNode == vc;
                bool validDir = _dragStartIsOutput != e.IsOutput;

                if (validDir && !(sameNode && _dragStartPort == e.PortName))
                    CreateConnection(_dragStartNode, vc, _dragStartPort, e.PortName, _dragStartIsOutput);

                if (_dragLine != null) { ConnectionsCanvas.Children.Remove(_dragLine); _dragLine = null; }
                _isDraggingConnection = false;
                _dragStartNode = null;

                // After connection, recheck warnings
                CheckAndShowWarnings();
            };
        }

        private void CreateConnection(NodeControl outputNode, NodeControl inputNode,
            string outputPort, string inputPort, bool startIsOutput)
        {
            string outUUID = startIsOutput ? outputNode.Data.UUID : inputNode.Data.UUID;
            string outPort = startIsOutput ? outputPort : inputPort;
            string inUUID  = startIsOutput ? inputNode.Data.UUID  : outputNode.Data.UUID;
            string inPort  = startIsOutput ? inputPort : outputPort;

            var outData = CurrentSession.Nodes.FirstOrDefault(n => n.UUID == outUUID);
            var inData  = CurrentSession.Nodes.FirstOrDefault(n => n.UUID == inUUID);
            if (outData == null || inData == null)
            {
                System.Diagnostics.Debug.WriteLine("[CONN] rejected: missing node data.");
                return;
            }

            var outPort_ = outData.Outputs.FirstOrDefault(o => o.Name == outPort);
            var inPort_  = inData.Inputs.FirstOrDefault(i => i.Name == inPort);
            if (outPort_ == null || inPort_ == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CONN] rejected: port not found ({outPort} / {inPort}).");
                return;
            }

            // Normalise semantic types so the comparison isn't tripped up by
            // null/casing/whitespace coming back from JSON load. "object"
            // accepts anything; "custom" only accepts another custom port
            // when the underlying type names match (or one side is unspecified).
            string a = (outPort_.SemanticType ?? "object").Trim().ToLowerInvariant();
            string b = (inPort_.SemanticType  ?? "object").Trim().ToLowerInvariant();
            // Any numeric-family tag connects to any other numeric-family tag.
            // (The codegen layer keeps each precision distinct on its own
            // node — this is just for wire compatibility.)
            bool numeric(string t) =>
                   t == "number" || t == "int" || t == "long"
                || t == "float"  || t == "decimal";

            bool typeMatch =
                   a == b
                || a == "object" || b == "object"
                || (numeric(a) && numeric(b))
                || (a == "custom" && b == "custom" &&
                    (string.IsNullOrEmpty(outPort_.CustomTypeName)
                     || string.IsNullOrEmpty(inPort_.CustomTypeName)
                     || outPort_.CustomTypeName == inPort_.CustomTypeName));

            if (!typeMatch)
            {
                System.Diagnostics.Debug.WriteLine($"[CONN] rejected: type mismatch {a} → {b}.");
                return;
            }

            try
            {
                NodeOperations.ConnectNode(CurrentSession, new Connection(outUUID, inUUID, outPort, inPort));
                Dispatcher.BeginInvoke(new Action(RefreshConnections), System.Windows.Threading.DispatcherPriority.Loaded);
                MarkUnsavedChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONN] {ex.Message}");
            }
        }

        private int _refreshRetryCount;
        private const int _refreshMaxRetries = 8;

        // Persistent map of Connection → its visual control. Reusing the
        // ConnectionControl lets us mutate the bezier path data in place
        // instead of clearing the whole canvas every drag tick (which is
        // what made connections blink out of existence while moving nodes).
        private readonly Dictionary<Connection, ConnectionControl> _connViews = new();

        private void RefreshConnections()
        {
            if (CurrentSession?.Connections == null)
            {
                ConnectionsCanvas.Children.Clear();
                _connViews.Clear();
                _refreshRetryCount = 0;
                return;
            }

            // Drop visuals whose underlying Connection was deleted.
            var live = new HashSet<Connection>(CurrentSession.Connections);
            foreach (var kv in _connViews.Where(kv => !live.Contains(kv.Key)).ToList())
            {
                ConnectionsCanvas.Children.Remove(kv.Value);
                _connViews.Remove(kv.Key);
            }

            bool anyDeferred = false;
            foreach (var conn in live)
            {
                var outCtrl = FindNodeByUUID(conn.Node1UUID);
                var inCtrl  = FindNodeByUUID(conn.Node2UUID);
                if (outCtrl == null || inCtrl == null) continue;

                // If a node hasn't been measured yet, defer and retry — but
                // DON'T tear the existing visual down in the meantime.
                if (outCtrl.ActualWidth == 0 || inCtrl.ActualWidth == 0)
                {
                    anyDeferred = true;
                    continue;
                }

                try
                {
                    var startPos = outCtrl.GetPortPosition(conn.Node1Port, true,  WorkspaceCanvas);
                    var endPos   = inCtrl.GetPortPosition( conn.Node2Port, false, WorkspaceCanvas);

                    if (_connViews.TryGetValue(conn, out var existing))
                    {
                        // Mutate in place — no removal, no flicker.
                        existing.UpdateGeometry(BuildBezierGeometry(startPos.X, startPos.Y, endPos.X, endPos.Y));
                    }
                    else
                    {
                        var path = DrawBezier(startPos.X, startPos.Y, endPos.X, endPos.Y);
                        var cc = new ConnectionControl(conn, path);
                        ConnectionsCanvas.Children.Add(cc);
                        _connViews[conn] = cc;
                    }
                }
                catch { }
            }

            // Keep retrying until every connection has been drawn, or we've exhausted
            // the retry budget (prevents infinite re-entry if a node never measures).
            if (anyDeferred && _refreshRetryCount < _refreshMaxRetries)
            {
                _refreshRetryCount++;
                Dispatcher.BeginInvoke(new Action(RefreshConnections),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                _refreshRetryCount = 0;
            }
        }

        private static PathGeometry BuildBezierGeometry(double x1, double y1, double x2, double y2)
        {
            double cp = Math.Max(60, Math.Abs(x2 - x1) * 0.5);
            var fig = new PathFigure { StartPoint = new Point(x1, y1) };
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Point(x1 + cp, y1),
                Point2 = new Point(x2 - cp, y2),
                Point3 = new Point(x2, y2)
            });
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
        }

        private static System.Windows.Shapes.Path DrawBezier(double x1, double y1, double x2, double y2)
        {
            return new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                Data = BuildBezierGeometry(x1, y1, x2, y2)
            };
        }

        private NodeControl FindNodeByUUID(string uuid)
        {
            foreach (UIElement child in WorkspaceCanvas.Children)
                if (child is NodeControl nc && nc.Data?.UUID == uuid) return nc;
            return null;
        }

        public void DeleteConnection(Connection connection)
        {
            NodeOperations.DisconnectNode(CurrentSession, connection);
            RefreshConnections();
            DeselectAll();
            MarkUnsavedChanges();
        }

        public void SelectConnection(ConnectionControl cc)
        {
            DeselectAllConnections();
            DeselectAllNodes();
            _selectedConnection = cc;
        }

        private void AddNewNote()
        {
            var note = new NoteControl();
            SetupNoteEvents(note);
            NotesCanvas.Children.Add(note);
            Canvas.SetLeft(note, _lastContextMenuPosition.X);
            Canvas.SetTop(note,  _lastContextMenuPosition.Y);
            MarkUnsavedChanges();
        }

        private void SetupNoteEvents(NoteControl note)
        {
            note.OnDragDelta += delta =>
            {
                foreach (var sel in _selectedNotes)
                {
                    if (sel == note) continue;
                    var l = Canvas.GetLeft(sel); var t = Canvas.GetTop(sel);
                    Canvas.SetLeft(sel, (double.IsNaN(l) ? 0 : l) + delta.X);
                    Canvas.SetTop(sel,  (double.IsNaN(t) ? 0 : t) + delta.Y);
                }
                MarkUnsavedChanges();
            };

            note.MouseLeftButtonDown += (s, e) =>
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    note.Selected = !note.Selected;
                    if (note.Selected) _selectedNotes.Add(note); else _selectedNotes.Remove(note);
                }
                else if (!note.Selected)
                {
                    DeselectAll(); note.Selected = true; _selectedNotes.Add(note);
                }
                e.Handled = true;
            };

            note.MouseRightButtonDown += (s, e) =>
            {
                ShowNoteContextMenu(note);
                e.Handled = true;
            };
        }

        private void ShowNoteContextMenu(NoteControl note)
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                BorderThickness = new Thickness(1),
                PlacementTarget = note,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
            };

            var del = new MenuItem { Header = "Delete Note" };
            del.Click += (s, e) =>
            {
                _selectedNotes.Remove(note);
                NotesCanvas.Children.Remove(note);
                MarkUnsavedChanges();
            };
            menu.Items.Add(del);

            note.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void AddNewChunk()
        {
            var chunk = new ChunkControl();
            chunk.ChunkTitle = "Does A Cool Thing";
            chunk.SetInitialSize(620, 420);
            SetupChunkEvents(chunk);
            ChunksCanvas.Children.Add(chunk);
            Canvas.SetLeft(chunk, _lastContextMenuPosition.X);
            Canvas.SetTop(chunk,  _lastContextMenuPosition.Y);
            MarkUnsavedChanges();
        }

        private void SetupChunkEvents(ChunkControl chunk)
        {
            chunk.OnDragDelta += delta =>
            {
                foreach (var sel in _selectedChunks)
                {
                    if (sel == chunk) continue;
                    var l = Canvas.GetLeft(sel); var t = Canvas.GetTop(sel);
                    Canvas.SetLeft(sel, (double.IsNaN(l) ? 0 : l) + delta.X);
                    Canvas.SetTop(sel,  (double.IsNaN(t) ? 0 : t) + delta.Y);
                }
                MarkUnsavedChanges();
            };

            chunk.MouseLeftButtonDown += (s, e) =>
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    chunk.Selected = !chunk.Selected;
                    if (chunk.Selected) _selectedChunks.Add(chunk); else _selectedChunks.Remove(chunk);
                }
                else if (!chunk.Selected)
                {
                    DeselectAll(); chunk.Selected = true; _selectedChunks.Add(chunk);
                }
                e.Handled = true;
            };

            chunk.MouseRightButtonDown += (s, e) =>
            {
                ShowChunkContextMenu(chunk);
                e.Handled = true;
            };
        }

        private static readonly Color[] _chunkColors = {
            Color.FromRgb(255,76,76),  Color.FromRgb(255,152,0), Color.FromRgb(255,213,79),
            Color.FromRgb(76,175,80),  Color.FromRgb(0,188,212), Color.FromRgb(33,150,243),
            Color.FromRgb(156,39,176), Color.FromRgb(96,125,139)
        };

        // Import EF / POCO entity from a pasted C# class. Adds (or updates)
        // the entity in CurrentSession.ImportedEntities, generates a "<Name>: New"
        // template node visible in the sidebar's Custom Nodes section, and
        // sweeps existing nodes derived from this entity to flag schema drift.
        private void ShowEntityImportDialog()
        {
            var box = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                Height = 220, Width = 420,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                Text =
                    "public class User\n" +
                    "{\n" +
                    "    public int Id { get; set; }\n" +
                    "    public string Email { get; set; }\n" +
                    "    public string PasswordHash { get; set; }\n" +
                    "}"
            };

            ShowMiniDialog("Import Entity", dialog =>
            {
                dialog.Children.Add(new TextBlock
                {
                    Text = "Paste a C# class. Properties become inputs on a "
                         + "\"<Name>: New\" template node. Re-import the same "
                         + "class with changes to bump its version — graph "
                         + "nodes that reference removed/changed properties "
                         + "will be flagged.",
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                dialog.Children.Add(box);
                AddButtons(dialog, Color.FromRgb(255, 76, 76), "Import", () =>
                {
                    var defs = ParseEntityClasses(box.Text);
                    foreach (var def in defs) ApplyEntityImport(def);
                });
            });
        }

        private static NodeWalker.SessionData.EntityDef ParseEntityClass(string source)
        {
            var defs = ParseEntityClasses(source);
            return defs.Count > 0 ? defs[0] : null;
        }

        /// <summary>
        /// Parse one or more C# class definitions out of a single paste. Used
        /// by Import Entity, but also tolerant of the multi-class output that
        /// Database Designer produces (a whole file with attribute lines,
        /// navigation properties, nullable reference types, and an attached
        /// AppDbContext at the bottom). DbContext / non-entity classes are
        /// skipped automatically by the navigation-property heuristic.
        /// </summary>
        private static List<NodeWalker.SessionData.EntityDef> ParseEntityClasses(string source)
        {
            var result = new List<NodeWalker.SessionData.EntityDef>();
            if (string.IsNullOrWhiteSpace(source)) return result;

            // Strip attribute lines / blocks ([Table("…")], [Key], [ForeignKey(…)]).
            // Multiline so a [Foo]\npublic class Bar still leaves the class line intact.
            source = System.Text.RegularExpressions.Regex.Replace(
                source, @"^[ \t]*\[[^\]\r\n]+\][ \t]*\r?\n",
                "", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Find every "class <Name>" + the matching brace block.
            var classRx = new System.Text.RegularExpressions.Regex(
                @"\bclass\s+(\w+)\b[^{]*\{",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (System.Text.RegularExpressions.Match m in classRx.Matches(source))
            {
                var name = m.Groups[1].Value;
                int bodyStart = m.Index + m.Length;
                int bodyEnd = FindMatchingBrace(source, bodyStart - 1);
                if (bodyEnd < 0) continue;
                var body = source.Substring(bodyStart, bodyEnd - bodyStart);

                // Skip DbContext-style classes.
                if (System.Text.RegularExpressions.Regex.IsMatch(body, @"\bDbSet<")) continue;
                if (name.EndsWith("DbContext", StringComparison.Ordinal)) continue;

                var props = new Dictionary<string, string>(StringComparer.Ordinal);
                var propRx = new System.Text.RegularExpressions.Regex(
                    @"public\s+([\w\.\?<>,\s]+?)\s+(\w+)\s*\{\s*get",
                    System.Text.RegularExpressions.RegexOptions.Multiline);

                foreach (System.Text.RegularExpressions.Match pm in propRx.Matches(body))
                {
                    var typeName = System.Text.RegularExpressions.Regex.Replace(
                        pm.Groups[1].Value.Trim(), @"\s+", "");
                    var propName = pm.Groups[2].Value;

                    if (string.IsNullOrEmpty(typeName)) continue;
                    if (typeName.StartsWith("class") || typeName == "static") continue;

                    // Skip navigation properties — collection types, or anything
                    // that's plainly a reference to another model class. We can't
                    // tell those apart with 100% certainty without a real C#
                    // parser, so use a heuristic: if the type name starts with
                    // an uppercase letter, has no generics, and isn't one of the
                    // known scalar names, treat it as a navigation prop.
                    if (IsCollectionType(typeName)) continue;
                    if (IsNavReference(typeName)) continue;

                    // Trim trailing nullability — node ports don't track it.
                    var clean = typeName.TrimEnd('?');
                    props[propName] = clean;
                }

                if (props.Count == 0) continue;

                var sig = string.Join("|", props.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value));
                result.Add(new NodeWalker.SessionData.EntityDef
                {
                    Name = name,
                    Properties = props,
                    Hash = SimpleHash(sig)
                });
            }

            return result;
        }

        private static int FindMatchingBrace(string s, int openBraceIndex)
        {
            int depth = 0;
            for (int i = openBraceIndex; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static bool IsCollectionType(string typeName) =>
               typeName.StartsWith("ICollection<", StringComparison.Ordinal)
            || typeName.StartsWith("IEnumerable<", StringComparison.Ordinal)
            || typeName.StartsWith("IList<",       StringComparison.Ordinal)
            || typeName.StartsWith("List<",        StringComparison.Ordinal)
            || typeName.StartsWith("HashSet<",     StringComparison.Ordinal)
            || typeName.EndsWith("[]",             StringComparison.Ordinal);

        private static readonly HashSet<string> _scalarTypeNames = new(StringComparer.Ordinal)
        {
            "bool", "byte", "sbyte", "char", "short", "ushort", "int", "uint",
            "long", "ulong", "float", "double", "decimal", "string", "object",
            "Guid", "DateTime", "DateOnly", "TimeOnly", "DateTimeOffset",
            "TimeSpan", "Uri",
            "JsonDocument", "JsonElement", "JsonObject",
            // Common fully-qualified scalars from DBDesigner's mappings:
            "System.Text.Json.JsonDocument", "NpgsqlTypes.NpgsqlPoint"
        };

        private static bool IsNavReference(string typeName)
        {
            // Generic / array / nullable-of-scalar all stay as-is (so they
            // become a port). Only flag bare "PascalCase" types that aren't
            // a scalar — those are almost certainly a sibling entity.
            if (typeName.IndexOfAny(new[] { '<', '[', '?', '.' }) >= 0)
            {
                // If a "?" suffix on a known scalar → keep.
                var bare = typeName.TrimEnd('?');
                if (_scalarTypeNames.Contains(bare)) return false;
                // Generics / arrays already filtered by IsCollectionType.
                return false;
            }
            if (_scalarTypeNames.Contains(typeName)) return false;
            return char.IsUpper(typeName[0]);
        }

        private static string SimpleHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var c in s) { h ^= c; h *= 16777619; }
                return h.ToString("x8");
            }
        }

        private void ApplyEntityImport(NodeWalker.SessionData.EntityDef def)
        {
            CurrentSession.ImportedEntities ??= new List<NodeWalker.SessionData.EntityDef>();
            var existing = CurrentSession.ImportedEntities.FirstOrDefault(e => e.Name == def.Name);

            int version = 1;
            if (existing != null)
            {
                if (existing.Hash == def.Hash) return; // no change
                version = existing.Version + 1;
                SweepEntityNodes(def, existing, version);
                CurrentSession.ImportedEntities.Remove(existing);
            }
            def.Version = version;
            CurrentSession.ImportedEntities.Add(def);

            // Replace any existing template in _customScripts with the new one
            var templateTitle = $"{def.Name}: New";
            _customScripts.RemoveAll(n => n.Title == templateTitle);
            _customScripts.Add(BuildEntityTemplate(def));
            SetupSidebar();
            CheckAndShowWarnings();
            MarkUnsavedChanges();
        }

        private BareNode BuildEntityTemplate(NodeWalker.SessionData.EntityDef def)
        {
            var inputs = new HashSet<Input>();
            foreach (var kv in def.Properties)
                inputs.Add(new Input(kv.Key, GetTypeFromString(kv.Value),
                    GetSemanticType(kv.Value), required: false, customTypeName: kv.Value));

            var outputs = new HashSet<Output>
            {
                new Output("Entity", typeof(object), "custom", def.Name)
            };

            return new BareNode
            {
                Title = $"{def.Name}: New",
                Description = $"Construct {def.Name} (v{def.Version})",
                Inputs = inputs,
                Outputs = outputs,
                UUID = Guid.NewGuid().ToString(),
                Logic = $"ENTITY:{def.Name}:{def.Hash}",
                SyncType = "Sync"
            };
        }

        private void SweepEntityNodes(NodeWalker.SessionData.EntityDef next,
            NodeWalker.SessionData.EntityDef prev, int newVersion)
        {
            var prefix = $"{next.Name}:";
            foreach (var node in CurrentSession.Nodes)
            {
                if (node.Title == null || !node.Title.StartsWith(prefix, StringComparison.Ordinal)) continue;

                // Inputs whose property no longer exists, or whose type changed.
                var bad = new List<string>();
                foreach (var inp in node.Inputs)
                {
                    if (!next.Properties.TryGetValue(inp.Name, out var newType))
                    { bad.Add($"removed: {inp.Name}"); continue; }
                    if (prev.Properties.TryGetValue(inp.Name, out var oldType) && oldType != newType)
                        bad.Add($"type change: {inp.Name} {oldType} → {newType}");
                }

                if (bad.Count > 0)
                {
                    node.Logic = $"ERROR: {next.Name} v{newVersion} schema drift: {string.Join("; ", bad)}";
                }
                else if (node.Logic != null && node.Logic.StartsWith("ENTITY:", StringComparison.Ordinal))
                {
                    // Bump the embedded hash so the marker stays consistent.
                    node.Logic = $"ENTITY:{next.Name}:{next.Hash}";
                }
            }
        }

        private void ShowChunkColorPicker(ChunkControl chunk)
        {
            ShowMiniDialog("Pick Group Colour", dialog =>
            {
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                int col = 0, row = 0;
                foreach (var c in _chunkColors)
                {
                    while (grid.RowDefinitions.Count <= row)
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var swatch = new Button
                    {
                        Width = 56, Height = 36,
                        Margin = new Thickness(4),
                        Background = new SolidColorBrush(c),
                        BorderThickness = chunk.ChunkBorderColor == c
                            ? new Thickness(3)
                            : new Thickness(1),
                        BorderBrush = chunk.ChunkBorderColor == c
                            ? Brushes.White
                            : new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                        Cursor = Cursors.Hand
                    };
                    var captured = c;
                    swatch.Click += (s, e) =>
                    {
                        chunk.ChunkBorderColor = captured;
                        MarkUnsavedChanges();
                        CloseAllOverlays();
                    };
                    Grid.SetColumn(swatch, col);
                    Grid.SetRow(swatch, row);
                    grid.Children.Add(swatch);

                    col++;
                    if (col >= 4) { col = 0; row++; }
                }
                dialog.Children.Add(grid);
            });
        }

        private void ShowChunkContextMenu(ChunkControl chunk)
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                BorderThickness = new Thickness(1),
                PlacementTarget = chunk,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
            };

            var rename = new MenuItem { Header = "Rename…" };
            rename.Click += (s, e) =>
                ShowMiniDialog("Rename Group", d =>
                {
                    var tb = AddLabeledTextBox(d, "Name:", chunk.ChunkTitle);
                    AddButtons(d, Color.FromRgb(255, 76, 76), "Rename", () =>
                    {
                        if (!string.IsNullOrWhiteSpace(tb.Text)) chunk.ChunkTitle = tb.Text;
                        MarkUnsavedChanges();
                    });
                });
            menu.Items.Add(rename);

            // Colour picker — opens a dialog of clickable swatches. Trying to
            // colourise MenuItem.Foreground or stuff complex content into a
            // submenu's Header is unreliable in OpenSilver, so we route to a
            // plain dialog with regular Buttons that we know renders correctly.
            var colorItem = new MenuItem { Header = "Color…" };
            colorItem.Click += (s, e) => ShowChunkColorPicker(chunk);
            menu.Items.Add(colorItem);

            menu.Items.Add(new Separator());
            var del = new MenuItem { Header = "Delete Group" };
            del.Click += (s, e) => { _selectedChunks.Remove(chunk); ChunksCanvas.Children.Remove(chunk); MarkUnsavedChanges(); };
            menu.Items.Add(del);

            chunk.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void MoveChunkToBack(FrameworkElement element)
        {
            var parent = VisualTreeHelper.GetParent(element) as Panel;
            if (parent == null) return;
            parent.Children.Remove(element);
            int idx = -1;
            for (int i = 0; i < parent.Children.Count; i++)
                if (parent.Children[i] is ChunkControl) { idx = i; break; }
            parent.Children.Insert(idx >= 0 ? idx : 0, element);
        }

        // Script parser (for "Create Custom Node")

        private BareNode ParseScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script)) return null;
            script = script.Trim();

            var methodMatch = System.Text.RegularExpressions.Regex.Match(script,
                @"^(?:public|private|protected|internal|static)?\s*(?:async)?\s*(\w+)\s+(\w+)\s*\((.*)\)");

            if (methodMatch.Success)
            {
                var returnType = methodMatch.Groups[1].Value;
                var methodName = methodMatch.Groups[2].Value;
                var parameters = methodMatch.Groups[3].Value;
                var inputs = new List<Input>();
                var outputs = new List<Output>();

                if (!string.IsNullOrWhiteSpace(parameters))
                {
                    foreach (var param in parameters.Split(','))
                    {
                        var pm = System.Text.RegularExpressions.Regex.Match(param.Trim(), @"^(\w+)\s+(\w+)");
                        if (pm.Success)
                            inputs.Add(new Input(pm.Groups[2].Value, GetTypeFromString(pm.Groups[1].Value), GetSemanticType(pm.Groups[1].Value), false));
                    }
                }

                if (returnType != "void")
                    outputs.Add(new Output("Result", GetTypeFromString(returnType), GetSemanticType(returnType)));

                return new BareNode
                {
                    Title = methodName,
                    Description = $"Generated from: {script}",
                    Inputs = new HashSet<Input>(inputs),
                    Outputs = new HashSet<Output>(outputs),
                    UUID = Guid.NewGuid().ToString(),
                    Logic = $"// {methodName} — implement here",
                    SyncType = "Sync"
                };
            }

            // Property
            var propMatch = System.Text.RegularExpressions.Regex.Match(script,
                @"^(?:public|private|protected)?\s*(\w+)\s+(\w+)\s*\{\s*get");
            if (propMatch.Success)
            {
                return new BareNode
                {
                    Title = propMatch.Groups[2].Value,
                    Description = $"Property: {script}",
                    Inputs = new HashSet<Input>(),
                    Outputs = new HashSet<Output> { new Output("Value", GetTypeFromString(propMatch.Groups[1].Value), GetSemanticType(propMatch.Groups[1].Value)) },
                    UUID = Guid.NewGuid().ToString(),
                    Logic = $"// {propMatch.Groups[2].Value} property",
                    SyncType = "Sync"
                };
            }

            // Constructor call: TypeName(param1 type1, ...) or bare TypeName
            var ctorCall = System.Text.RegularExpressions.Regex.Match(script, @"^(\w[\w<>]*)\s*\((.*)\)$");
            if (ctorCall.Success)
                return BuildCtorNode(ctorCall.Groups[1].Value, ctorCall.Groups[2].Value);

            var bareType = System.Text.RegularExpressions.Regex.Match(script, @"^(\w[\w<>]*)$");
            if (bareType.Success)
                return BuildCtorNode(bareType.Groups[1].Value, "");

            return null;
        }

        private BareNode BuildCtorNode(string typeName, string rawParams)
        {
            var inputs = new List<Input>();
            int idx = 0;
            if (!string.IsNullOrWhiteSpace(rawParams))
                foreach (var p in rawParams.Split(','))
                {
                    var pm = System.Text.RegularExpressions.Regex.Match(p.Trim(), @"^([\w<>\[\]]+)\s+(\w+)");
                    inputs.Add(pm.Success
                        ? new Input(pm.Groups[2].Value, GetTypeFromString(pm.Groups[1].Value), GetSemanticType(pm.Groups[1].Value), false)
                        : new Input($"arg{idx}", typeof(object), "object", false));
                    idx++;
                }
            return new BareNode
            {
                Title       = $"new {typeName}",
                Description = $"Constructs a {typeName} instance",
                Inputs      = new HashSet<Input>(inputs),
                Outputs     = new HashSet<Output> { new Output("Instance", typeof(object), typeName) },
                UUID        = Guid.NewGuid().ToString(),
                Logic       = $"var result = new {typeName}({string.Join(", ", inputs.Select(i => i.Name))});",
                SyncType    = "Sync"
            };
        }

        // Map a type name (CLR or DBDesigner-style) → CLR Type. Used when
        // hydrating Input/Output instances on import. Preserves precision
        // (no more "every numeric becomes int" bucketing).
        private Type GetTypeFromString(string t) => t.Trim().TrimEnd('?').ToLower() switch
        {
            "byte" or "sbyte"            => typeof(byte),
            "short" or "ushort"          => typeof(short),
            "int" or "uint" or "int32"   => typeof(int),
            "long" or "ulong" or "int64" => typeof(long),
            "float" or "single"          => typeof(float),
            "double"                     => typeof(double),
            "decimal"                    => typeof(decimal),
            "string"                     => typeof(string),
            "bool" or "boolean"          => typeof(bool),
            "guid"                       => typeof(Guid),
            "datetime"                   => typeof(DateTime),
            "datetimeoffset"             => typeof(DateTimeOffset),
            "dateonly"                   => typeof(DateOnly),
            "timeonly"                   => typeof(TimeOnly),
            "timespan"                   => typeof(TimeSpan),
            "uri"                        => typeof(Uri),
            "byte[]"                     => typeof(byte[]),
            _                            => typeof(object)
        };

        // Map to a NODEWLKR semantic-type tag. Each precision bucket gets its
        // own tag so the codegen layer can emit the correct CLR keyword
        // instead of widening everything to `double`.
        private string GetSemanticType(string t) => t.Trim().TrimEnd('?').ToLower() switch
        {
            "byte" or "sbyte" or "short" or "ushort"
                or "int" or "uint" or "int32"        => "int",
            "long" or "ulong" or "int64"             => "long",
            "float" or "single"                      => "float",
            "double"                                 => "number",
            "decimal"                                => "decimal",
            "string"                                 => "string",
            "bool" or "boolean"                      => "bool",
            "guid"                                   => "guid",
            "datetime"                               => "datetime",
            "datetimeoffset"                         => "datetimeoffset",
            "dateonly"                               => "dateonly",
            "timeonly"                               => "timeonly",
            "timespan"                               => "timespan",
            "uri"                                    => "weburl",
            "byte[]"                                 => "bytes",
            _                                        => "object"
        };

        private Type GetTypeFromSemantic(string sem) => sem switch
        {
            "int"            => typeof(int),
            "long"           => typeof(long),
            "float"          => typeof(float),
            "number"         => typeof(double),
            "decimal"        => typeof(decimal),
            "string"         => typeof(string),
            "bool"           => typeof(bool),
            "guid"           => typeof(Guid),
            "datetime"       => typeof(DateTime),
            "datetimeoffset" => typeof(DateTimeOffset),
            "dateonly"       => typeof(DateOnly),
            "timeonly"       => typeof(TimeOnly),
            "timespan"       => typeof(TimeSpan),
            "weburl"         => typeof(Uri),
            "bytes"          => typeof(byte[]),
            _                => typeof(object)
        };

        private void ShowMiniDialog(string title, Action<StackPanel> buildContent,
            Color? accent = null, bool closeOnAction = true)
        {
            var accentColor = accent ?? Color.FromRgb(255, 76, 76);
            Grid overlay = null;

            overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) };

            var dialog = new StackPanel { Width = 360 };

            // Title row with a close button on the right.
            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            titleRow.Children.Add(new TextBlock
            {
                Text = title, FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var closeBtn = new Button
            {
                Content = "✕",
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand,
                FontSize = 14
            };
            closeBtn.Click += (s, e) => CloseDialog(overlay);
            titleRow.Children.Add(closeBtn);
            dialog.Children.Add(titleRow);

            buildContent(dialog);

            var popup = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                BorderBrush = new SolidColorBrush(accentColor),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(22),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new ScrollViewer
                {
                    Content = dialog,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 560
                }
            };

            overlay.Children.Add(popup);

            // Click on the dimmed background closes the dialog.
            overlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == overlay) CloseDialog(overlay);
            };

            // ESC closes the dialog.
            KeyEventHandler escHandler = null;
            escHandler = (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CloseDialog(overlay);
                    KeyDown -= escHandler;
                    e.Handled = true;
                }
            };
            KeyDown += escHandler;

            var mainGrid = this.Content as Grid;
            if (mainGrid != null) { overlay.SetValue(Grid.RowSpanProperty, 3); mainGrid.Children.Add(overlay); }
        }

        private static void CloseDialog(Grid overlay)
        {
            (overlay.Parent as Grid)?.Children.Remove(overlay);
        }

        private TextBox AddLabeledTextBox(StackPanel parent, string label, string defaultText)
        {
            parent.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) });
            var box = new TextBox
            {
                Text = defaultText,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                Padding = new Thickness(8, 5, 8, 5),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            parent.Children.Add(box);
            return box;
        }

        private static void AddSectionLabel(StackPanel parent, string text)
        {
            parent.Children.Add(new TextBlock
            {
                Text = text, Foreground = Brushes.White,
                FontSize = 12, Margin = new Thickness(0, 0, 0, 4)
            });
        }

        private (ComboBox typeBox, TextBox customBox) AddTypeSelector(StackPanel parent, string label)
        {
            parent.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) });

            var typeBox = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            foreach (var t in _baseTypes) typeBox.Items.Add(t);
            typeBox.SelectedIndex = 0;
            parent.Children.Add(typeBox);

            var customBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                Padding = new Thickness(8, 5, 8, 5),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed,
                Text = ""
            };
            customBox.SetValue(TextBox.PlaceholderTextProperty, "Enter custom type name (e.g. MyEntity)");
            parent.Children.Add(customBox);

            typeBox.SelectionChanged += (s, e) =>
                customBox.Visibility = typeBox.SelectedItem?.ToString() == "custom"
                    ? Visibility.Visible : Visibility.Collapsed;

            return (typeBox, customBox);
        }

        private string GetSelectedSemantic(ComboBox typeBox, TextBox customBox)
        {
            var s = typeBox.SelectedItem?.ToString() ?? "object";
            return s == "custom" && !string.IsNullOrWhiteSpace(customBox.Text)
                ? customBox.Text.Trim() : s;
        }

        private void AddButtons(StackPanel parent, Color accent, string confirmLabel, Action onConfirm)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            MakeBtn(row, confirmLabel, accent, () =>
            {
                onConfirm();
                // Find and close the overlay
                var overlay = FindOverlay(parent);
                if (overlay != null) CloseDialog(overlay);
            });
            MakeBtn(row, "Cancel", Color.FromRgb(68, 68, 68), () =>
            {
                var overlay = FindOverlay(parent);
                if (overlay != null) CloseDialog(overlay);
            });
            parent.Children.Add(row);
        }

        private void MakeBtn(StackPanel row, string text, Color color, Action action)
        {
            var btn = new Button
            {
                Content = text,
                Background = new SolidColorBrush(color),
                Foreground = Brushes.White,
                Padding = new Thickness(14, 7, 14, 7),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0),
                FontFamily = new FontFamily("/Database_Designer;component/Assets/NodeWalker/twoweekendgo-regular.ttf#Two Weekend Go")
            };
            if (action != null) btn.Click += (s, e) => action();
            row.Children.Add(btn);
        }

        private static Grid FindOverlay(DependencyObject d)
        {
            var p = VisualTreeHelper.GetParent(d);
            while (p != null)
            {
                if (p is Grid g && g.Background is SolidColorBrush sb && sb.Color.A < 220) return g;
                p = VisualTreeHelper.GetParent(p);
            }
            return null;
        }

        private void ShowScriptToNodeWindow() { /* legacy — handled inline */ }
    }

    public class Category
    {
        public string Name { get; set; }
        public List<BareNode> Nodes { get; set; } = new();
    }

    public static class CanvasExtensions
    {
        // Monotonically-increasing counter so each call wins the z-order race
        // without us ever having to reset everyone else.
        private static int _nextZ = 1000;

        public static void BringToFront(this UIElement element)
        {
            // The previous implementation removed and re-added the element to
            // its parent panel. That works visually, but Children.Remove blows
            // away any in-flight mouse capture — which means a node that was
            // mid-drag would silently stop receiving MouseMove events the
            // instant it became "selected" (because OnDragStarted calls this).
            // Use Canvas.ZIndex instead: same visual result, capture intact.
            Canvas.SetZIndex(element, System.Threading.Interlocked.Increment(ref _nextZ));
        }
    }

    public class ConnectionControl : Canvas
    {
        public Connection Connection { get; }
        private readonly System.Windows.Shapes.Path _path;
        private readonly System.Windows.Shapes.Path _hitArea;
        private bool _isSelected;

        public ConnectionControl(Connection connection, System.Windows.Shapes.Path path)
        {
            Connection = connection;
            _path = path;

            _hitArea = new System.Windows.Shapes.Path
            {
                Stroke = Brushes.Transparent,
                StrokeThickness = 14,
                Data = path.Data
            };
            Children.Add(_hitArea);
            Children.Add(path);

            MouseLeftButtonDown += (s, e) => { Select(); e.Handled = true; };
            MouseRightButtonDown += (s, e) => { Select(); ShowContextMenu(); e.Handled = true; };
        }

        /// <summary>
        /// Re-point both the visible bezier and the wider hit-test stroke at
        /// the new geometry. Lets RefreshConnections move connections during
        /// node drags without removing/recreating the visual element.
        /// </summary>
        public void UpdateGeometry(Geometry geometry)
        {
            _path.Data    = geometry;
            _hitArea.Data = geometry;
        }

        public void Select()
        {
            if (_isSelected) return;
            _isSelected = true;
            _path.Stroke = new SolidColorBrush(Color.FromRgb(76, 132, 255));
            _path.StrokeThickness = 3.5;
            FindMainPage()?.SelectConnection(this);
        }

        public void Deselect()
        {
            if (!_isSelected) return;
            _isSelected = false;
            _path.Stroke = new SolidColorBrush(Color.FromRgb(255, 76, 76));
            _path.StrokeThickness = 2.5;
        }

        private void ShowContextMenu()
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 76, 76)),
                BorderThickness = new Thickness(1)
            };
            var del = new MenuItem { Header = "Delete Connection" };
            del.Click += (s, e) => FindMainPage()?.DeleteConnection(Connection);
            menu.Items.Add(del);
            menu.IsOpen = true;
        }

        private NodeWalkerWindow FindMainPage()
        {
            var p = VisualTreeHelper.GetParent(this);
            while (p != null) { if (p is NodeWalkerWindow mp) return mp; p = VisualTreeHelper.GetParent(p); }
            return null;
        }
    }
}
