using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimilarPhotoFinder {
    public class MainWindow : Window {
        // State
        private int _currentMode = 0; // 0 = Album Cleaner, 1 = Reference Search
        private string _targetFolder = "";
        private List<string> _referenceFiles = new List<string>();
        private CancellationTokenSource _cts;

        private List<SimilarGroup> _albumGroups = new List<SimilarGroup>();
        private List<ReferenceMatchItem> _referenceMatches = new List<ReferenceMatchItem>();

        // Controls
        private TextBlock _headerTitle;
        private TextBlock _headerSubtitle;
        private Button _tabAlbumBtn;
        private Button _tabRefBtn;

        // Album mode controls
        private StackPanel _albumInputPanel;
        private TextBox _folderPathBox;
        private CheckBox _recursiveCheck;

        // Reference mode controls
        private StackPanel _refInputPanel;
        private WrapPanel _refThumbnailsPanel;
        private TextBox _refTargetFolderBox;

        // Common settings
        private Slider _sensitivitySlider;
        private TextBlock _sensitivityLabel;
        private Button _scanBtn;
        private Button _cancelBtn;

        // Progress panel
        private Border _progressBorder;
        private ProgressBar _progressBar;
        private TextBlock _progressStatusText;
        private TextBlock _progressFileText;

        // Results area
        private Border _toolbarBorder;
        private TextBlock _resultsStatsText;
        private ScrollViewer _resultsScrollViewer;
        private StackPanel _resultsContentPanel;

        // Bottom floating bar
        private Border _bottomFloatingBar;
        private CheckBox _recycleBinCheck;
        private TextBlock _selectedSummaryText;
        private Button _deleteSelectedBtn;

        public MainWindow() {
            Title = "Duplicates by S1mplee67";
            Width = 1080;
            Height = 780;
            MinWidth = 850;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(12, 14, 18));
            Foreground = Brushes.White;

            AllowDrop = true;
            Drop += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                    string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (dropped != null && dropped.Length > 0) {
                        if (Directory.Exists(dropped[0])) {
                            SetTargetFolder(dropped[0]);
                        } else if (File.Exists(dropped[0])) {
                            string ext = Path.GetExtension(dropped[0]).ToLowerInvariant();
                            if (new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif" }.Contains(ext)) {
                                if (_currentMode == 1) {
                                    foreach (var f in dropped) {
                                        if (!_referenceFiles.Contains(f)) _referenceFiles.Add(f);
                                    }
                                    RefreshReferenceChips();
                                } else {
                                    SetTargetFolder(Path.GetDirectoryName(dropped[0]));
                                }
                            }
                        }
                    }
                }
            };
            PreviewDragOver += (s, e) => {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            };

            BuildUI();

            // Default target folder to current directory or Pictures
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(currentDir)) {
                SetTargetFolder(currentDir);
            }
        }

        private void BuildUI() {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tabs & Settings
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Results Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scrollable Results
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Bar

            // 1. Header (Vivo Phone Style + S1mplee67 Branding)
            var headerBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(17, 20, 28)),
                Padding = new Thickness(24, 14, 24, 14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 38, 54)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerPanel = new DockPanel();

            // Right header controls: Author badge + About button + Reset button
            var headerRightPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var authorBadge = new Border {
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 73, 112)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 4, 12, 4),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            authorBadge.MouseLeftButtonDown += (s, e) => ShowAboutDialog();
            var authorBadgeStack = new StackPanel { Orientation = Orientation.Horizontal };
            authorBadgeStack.Children.Add(new TextBlock {
                Text = "✨ by S1mplee67",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 180, 252))
            });
            authorBadge.Child = authorBadgeStack;
            headerRightPanel.Children.Add(authorBadge);

            var aboutBtn = new Button {
                Content = "ℹ About",
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 54)),
                Foreground = Brushes.White,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            aboutBtn.Click += (s, e) => ShowAboutDialog();
            headerRightPanel.Children.Add(aboutBtn);

            var resetBtn = new Button {
                Content = "↻ Reset",
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush(Color.FromRgb(35, 40, 52)),
                Foreground = Brushes.White,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            resetBtn.Click += (s, e) => ResetUI();
            headerRightPanel.Children.Add(resetBtn);

            DockPanel.SetDock(headerRightPanel, Dock.Right);
            headerPanel.Children.Add(headerRightPanel);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _headerTitle = new TextBlock {
                Text = "‹ Duplicates by S1mplee67",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Cursor = Cursors.Hand
            };
            _headerTitle.MouseLeftButtonDown += (s, e) => ResetUI();

            _headerSubtitle = new TextBlock {
                Text = "Offline Visual Duplicate & Similar Photo Cleaner • Created by S1mplee67",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            titleStack.Children.Add(_headerTitle);
            titleStack.Children.Add(_headerSubtitle);
            headerPanel.Children.Add(titleStack);
            headerBorder.Child = headerPanel;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Settings & Tabs
            var configBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(20, 22, 29)),
                Padding = new Thickness(24, 16, 24, 16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 36, 48)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var configStack = new StackPanel();

            // Mode Selector Tabs
            var tabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            _tabAlbumBtn = CreateTabButton("📁 Whole Album Cleaner", true, () => SwitchMode(0));
            _tabRefBtn = CreateTabButton("🎯 Search by Reference Photo(s)", false, () => SwitchMode(1));
            tabsPanel.Children.Add(_tabAlbumBtn);
            tabsPanel.Children.Add(_tabRefBtn);
            configStack.Children.Add(tabsPanel);

            // Album Mode Panel
            _albumInputPanel = new StackPanel();
            var albumRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var browseAlbumBtn = new Button {
                Content = "Browse Folder...",
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(Color.FromRgb(40, 46, 62)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            browseAlbumBtn.Click += (s, e) => BrowseTargetFolder();
            DockPanel.SetDock(browseAlbumBtn, Dock.Right);
            albumRow.Children.Add(browseAlbumBtn);

            _folderPathBox = new TextBox {
                Height = 36,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(15, 17, 23)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 52, 68)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            _folderPathBox.AllowDrop = true;
            _folderPathBox.Drop += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0) {
                        string path = files[0];
                        if (File.Exists(path)) path = Path.GetDirectoryName(path);
                        SetTargetFolder(path);
                    }
                }
            };
            _folderPathBox.PreviewDragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            albumRow.Children.Add(_folderPathBox);
            _albumInputPanel.Children.Add(albumRow);
            configStack.Children.Add(_albumInputPanel);

            // Reference Mode Panel
            _refInputPanel = new StackPanel { Visibility = Visibility.Collapsed };
            var refSelectRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var chooseRefBtn = new Button {
                Content = "+ Choose Reference Photo(s)...",
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(Color.FromRgb(30, 64, 175)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            chooseRefBtn.Click += (s, e) => BrowseReferencePhotos();
            DockPanel.SetDock(chooseRefBtn, Dock.Left);
            refSelectRow.Children.Add(chooseRefBtn);

            var refHint = new TextBlock {
                Text = "Pick 1 or 2 photos to search for matching or similar photos in the album",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                FontSize = 12
            };
            refSelectRow.Children.Add(refHint);
            _refInputPanel.Children.Add(refSelectRow);

            _refThumbnailsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            _refInputPanel.Children.Add(_refThumbnailsPanel);

            // Reference Target Folder Row
            var refTargetRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var browseRefTargetBtn = new Button {
                Content = "Browse Album...",
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(Color.FromRgb(40, 46, 62)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            browseRefTargetBtn.Click += (s, e) => BrowseTargetFolder();
            DockPanel.SetDock(browseRefTargetBtn, Dock.Right);
            refTargetRow.Children.Add(browseRefTargetBtn);

            _refTargetFolderBox = new TextBox {
                Height = 36,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(15, 17, 23)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 52, 68)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            refTargetRow.Children.Add(_refTargetFolderBox);
            _refInputPanel.Children.Add(refTargetRow);

            configStack.Children.Add(_refInputPanel);

            // Common Settings Row: Sensitivity Slider + Scan Button
            var commonGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            commonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            commonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Sensitivity
            var sensPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var sensTitle = new TextBlock {
                Text = "Similarity Sensitivity: ",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 186, 202)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            _sensitivitySlider = new Slider {
                Minimum = 60,
                Maximum = 98,
                Value = 85,
                Width = 140,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            _sensitivityLabel = new TextBlock {
                Text = "85% (Balanced)",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 110
            };
            _sensitivitySlider.ValueChanged += (s, e) => {
                int val = (int)_sensitivitySlider.Value;
                string label = string.Format("{0}% (Balanced)", val);
                if (val >= 95) label = string.Format("{0}% (Exact Duplicates)", val);
                else if (val >= 88) label = string.Format("{0}% (Very Similar)", val);
                else if (val <= 72) label = string.Format("{0}% (Loose / Burst)", val);
                _sensitivityLabel.Text = label;
            };
            sensPanel.Children.Add(sensTitle);
            sensPanel.Children.Add(_sensitivitySlider);
            sensPanel.Children.Add(_sensitivityLabel);

            _recursiveCheck = new CheckBox {
                Content = "Include Subfolders",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 186, 202)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            sensPanel.Children.Add(_recursiveCheck);
            Grid.SetColumn(sensPanel, 0);
            commonGrid.Children.Add(sensPanel);

            // Action Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _cancelBtn = new Button {
                Content = "✕ Cancel",
                Height = 38,
                Width = 90,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            _cancelBtn.Click += (s, e) => CancelScan();
            btnPanel.Children.Add(_cancelBtn);

            _scanBtn = new Button {
                Content = "🔍  Start Scanning Offline",
                Height = 38,
                Padding = new Thickness(22, 0, 22, 0),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _scanBtn.Click += (s, e) => StartScan();
            btnPanel.Children.Add(_scanBtn);

            Grid.SetColumn(btnPanel, 2);
            commonGrid.Children.Add(btnPanel);

            configStack.Children.Add(commonGrid);
            configBorder.Child = configStack;
            Grid.SetRow(configBorder, 1);
            rootGrid.Children.Add(configBorder);

            // 3. Progress Overlay
            _progressBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 38)),
                Padding = new Thickness(24, 12, 24, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 44, 60)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Visibility = Visibility.Collapsed
            };
            var progressStack = new StackPanel();
            var progTop = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            _progressFileText = new TextBlock {
                Text = "",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(_progressFileText, Dock.Right);
            progTop.Children.Add(_progressFileText);

            _progressStatusText = new TextBlock {
                Text = "Analyzing photos...",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White
            };
            progTop.Children.Add(_progressStatusText);
            progressStack.Children.Add(progTop);

            _progressBar = new ProgressBar {
                Height = 8,
                Minimum = 0,
                Maximum = 100,
                Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Background = new SolidColorBrush(Color.FromRgb(40, 46, 62))
            };
            progressStack.Children.Add(_progressBar);
            _progressBorder.Child = progressStack;
            Grid.SetRow(_progressBorder, 2);
            rootGrid.Children.Add(_progressBorder);

            // 4. Results Toolbar
            _toolbarBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(16, 18, 24)),
                Padding = new Thickness(24, 10, 24, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 32, 42)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Visibility = Visibility.Collapsed
            };
            var toolDock = new DockPanel();

            var quickActions = new StackPanel { Orientation = Orientation.Horizontal };
            var autoSelectBtn = CreateToolbarButton("⚡ Auto-Select Duplicates (Keep Best)", () => AutoSelectDuplicates());
            var selectAllExceptFirstBtn = CreateToolbarButton("Select Duplicates (Keep 1st)", () => SelectDuplicatesExceptFirst());
            var deselectAllBtn = CreateToolbarButton("Clear Selection", () => DeselectAll());

            quickActions.Children.Add(autoSelectBtn);
            quickActions.Children.Add(selectAllExceptFirstBtn);
            quickActions.Children.Add(deselectAllBtn);
            DockPanel.SetDock(quickActions, Dock.Right);
            toolDock.Children.Add(quickActions);

            _resultsStatsText = new TextBlock {
                Text = "",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 186, 202)),
                VerticalAlignment = VerticalAlignment.Center
            };
            toolDock.Children.Add(_resultsStatsText);
            _toolbarBorder.Child = toolDock;
            Grid.SetRow(_toolbarBorder, 3);
            rootGrid.Children.Add(_toolbarBorder);

            // 5. Scrollable Results Area
            _resultsScrollViewer = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 16, 20, 80) // 80px bottom padding for floating bar
            };
            _resultsContentPanel = new StackPanel();

            // Default Welcome / Empty state
            ShowWelcomeEmptyState();

            _resultsScrollViewer.Content = _resultsContentPanel;
            Grid.SetRow(_resultsScrollViewer, 4);
            rootGrid.Children.Add(_resultsScrollViewer);

            // 6. Bottom Floating Bar (Vivo Phone Style)
            _bottomFloatingBar = new Border {
                Background = new SolidColorBrush(Color.FromRgb(18, 20, 26)),
                Padding = new Thickness(24, 12, 24, 14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 40, 52)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed
            };
            var bottomDock = new DockPanel();

            _deleteSelectedBtn = new Button {
                Content = "Delete",
                Height = 44,
                Padding = new Thickness(28, 0, 28, 0),
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsEnabled = false
            };
            _deleteSelectedBtn.Click += (s, e) => DeleteSelectedItems();
            DockPanel.SetDock(_deleteSelectedBtn, Dock.Right);
            bottomDock.Children.Add(_deleteSelectedBtn);

            var leftBottomStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _selectedSummaryText = new TextBlock {
                Text = "0 items selected (0 KB)",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            _recycleBinCheck = new CheckBox {
                Content = "Move to Recycle Bin (Safe & restorable)",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            };
            leftBottomStack.Children.Add(_selectedSummaryText);
            leftBottomStack.Children.Add(_recycleBinCheck);
            bottomDock.Children.Add(leftBottomStack);

            _bottomFloatingBar.Child = bottomDock;
            Grid.SetRow(_bottomFloatingBar, 5);
            rootGrid.Children.Add(_bottomFloatingBar);

            Content = rootGrid;
        }

        private void SetTargetFolder(string folder) {
            _targetFolder = folder;
            _folderPathBox.Text = folder;
            _refTargetFolderBox.Text = folder;
        }

        private void SwitchMode(int mode) {
            _currentMode = mode;
            if (mode == 0) {
                _tabAlbumBtn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                _tabRefBtn.Background = new SolidColorBrush(Color.FromRgb(28, 32, 42));
                _albumInputPanel.Visibility = Visibility.Visible;
                _refInputPanel.Visibility = Visibility.Collapsed;
            } else {
                _tabAlbumBtn.Background = new SolidColorBrush(Color.FromRgb(28, 32, 42));
                _tabRefBtn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                _albumInputPanel.Visibility = Visibility.Collapsed;
                _refInputPanel.Visibility = Visibility.Visible;
            }
        }

        private Button CreateTabButton(string text, bool active, Action onClick) {
            var btn = new Button {
                Content = text,
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(active ? Color.FromRgb(37, 99, 235) : Color.FromRgb(28, 32, 42)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Button CreateToolbarButton(string text, Action onClick) {
            var btn = new Button {
                Content = text,
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(6, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(35, 40, 54)),
                Foreground = Brushes.White,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void BrowseTargetFolder() {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog()) {
                dialog.Description = "Select Album / Photo Folder to Scan";
                dialog.SelectedPath = Directory.Exists(_targetFolder) ? _targetFolder : AppDomain.CurrentDomain.BaseDirectory;
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    SetTargetFolder(dialog.SelectedPath);
                }
            }
        }

        private void BrowseReferencePhotos() {
            using (var dialog = new System.Windows.Forms.OpenFileDialog()) {
                dialog.Title = "Select 1 or 2 Reference Photos to Search Duplicates";
                dialog.Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    foreach (var f in dialog.FileNames) {
                        if (!_referenceFiles.Contains(f)) {
                            _referenceFiles.Add(f);
                        }
                    }
                    RefreshReferenceChips();
                }
            }
        }

        private void RefreshReferenceChips() {
            _refThumbnailsPanel.Children.Clear();
            foreach (var file in _referenceFiles.ToList()) {
                var chipBorder = new Border {
                    Background = new SolidColorBrush(Color.FromRgb(28, 32, 44)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(48, 56, 76)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 4, 8, 4),
                    Margin = new Thickness(0, 0, 8, 8)
                };
                var chipStack = new StackPanel { Orientation = Orientation.Horizontal };

                try {
                    var thumb = new Image {
                        Source = ImageItem.LoadThumbnail(file, 64),
                        Width = 32,
                        Height = 32,
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    chipStack.Children.Add(thumb);
                } catch { }

                var nameText = new TextBlock {
                    Text = Path.GetFileName(file),
                    Foreground = Brushes.White,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 180,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                chipStack.Children.Add(nameText);

                var removeBtn = new Button {
                    Content = "✕",
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(8, 0, 0, 0),
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    FontWeight = FontWeights.Bold
                };
                string capturedFile = file;
                removeBtn.Click += (s, e) => {
                    _referenceFiles.Remove(capturedFile);
                    RefreshReferenceChips();
                };
                chipStack.Children.Add(removeBtn);

                chipBorder.Child = chipStack;
                _refThumbnailsPanel.Children.Add(chipBorder);
            }
        }

        private void ShowWelcomeEmptyState() {
            _resultsContentPanel.Children.Clear();

            var emptyStack = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
                MaxWidth = 720
            };

            var iconText = new TextBlock {
                Text = "📸",
                FontSize = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            emptyStack.Children.Add(iconText);

            var titleText = new TextBlock {
                Text = "Duplicates by S1mplee67",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            emptyStack.Children.Add(titleText);

            var bylineText = new TextBlock {
                Text = "Offline Visual Duplicate & Similar Photo Cleaner",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 180, 252)),
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            };
            emptyStack.Children.Add(bylineText);

            // Feature Badges
            var badgesPanel = new WrapPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            };
            string[] badges = new string[] {
                "🔒 100% Offline & Private",
                "🧠 Perceptual Pixel Matching",
                "📁 Vivo-Style Date Groups",
                "🛡️ Windows Recycle Bin Safe"
            };
            foreach (var b in badges) {
                var badgeBorder = new Border {
                    Background = new SolidColorBrush(Color.FromRgb(24, 28, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 76)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 5, 12, 5),
                    Margin = new Thickness(4)
                };
                badgeBorder.Child = new TextBlock {
                    Text = b,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 218, 235))
                };
                badgesPanel.Children.Add(badgeBorder);
            }
            emptyStack.Children.Add(badgesPanel);

            // Quick Start Card
            var guideBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(18, 21, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 16, 20, 16)
            };
            var guideStack = new StackPanel();
            guideStack.Children.Add(new TextBlock {
                Text = "Quick Start Guide",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            });
            string[] steps = new string[] {
                "1. Choose an album folder or select 1-2 reference photos to match against.",
                "2. Adjust Similarity Sensitivity (85% is ideal for duplicates and similar burst shots).",
                "3. Click Start Scanning Offline and wait for results.",
                "4. Compare side-by-side or use Auto-Select to clean up duplicate files safely."
            };
            foreach (var step in steps) {
                guideStack.Children.Add(new TextBlock {
                    Text = step,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 168, 186)),
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }
            guideBorder.Child = guideStack;
            emptyStack.Children.Add(guideBorder);

            _resultsContentPanel.Children.Add(emptyStack);
        }

        private async void StartScan() {
            string target = _currentMode == 0 ? _folderPathBox.Text.Trim() : _refTargetFolderBox.Text.Trim();
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target)) {
                MessageBox.Show(this, "Please choose a valid folder to scan.", "Folder Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentMode == 1 && _referenceFiles.Count == 0) {
                MessageBox.Show(this, "Please select at least 1 reference photo to search for matches.", "Reference Photo Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _cts = new CancellationTokenSource();
            _scanBtn.IsEnabled = false;
            _cancelBtn.Visibility = Visibility.Visible;
            _progressBorder.Visibility = Visibility.Visible;
            _toolbarBorder.Visibility = Visibility.Collapsed;
            _bottomFloatingBar.Visibility = Visibility.Collapsed;
            _progressBar.Value = 0;
            _progressStatusText.Text = "Starting offline pixel analysis...";
            _progressFileText.Text = "";

            double threshold = _sensitivitySlider.Value;
            bool recursive = _recursiveCheck.IsChecked == true;

            try {
                if (_currentMode == 0) {
                    // Whole Album Mode
                    var groups = await Scanner.ScanAlbumAsync(
                        target,
                        recursive,
                        threshold,
                        info => Dispatcher.Invoke(() => {
                            _progressBar.Value = info.Percentage;
                            _progressStatusText.Text = info.StatusMessage;
                            _progressFileText.Text = info.CurrentFileName;
                        }),
                        _cts.Token
                    );

                    _albumGroups = groups;
                    RenderAlbumResults(groups);
                } else {
                    // Reference Search Mode
                    var matches = await Scanner.ScanByReferenceAsync(
                        _referenceFiles,
                        target,
                        recursive,
                        threshold,
                        info => Dispatcher.Invoke(() => {
                            _progressBar.Value = info.Percentage;
                            _progressStatusText.Text = info.StatusMessage;
                            _progressFileText.Text = info.CurrentFileName;
                        }),
                        _cts.Token
                    );

                    _referenceMatches = matches;
                    RenderReferenceResults(matches);
                }
            } catch (Exception ex) {
                MessageBox.Show(this, "Scan error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                _scanBtn.IsEnabled = true;
                _cancelBtn.Visibility = Visibility.Collapsed;
                _progressBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelScan() {
            if (_cts != null) {
                _cts.Cancel();
                _progressStatusText.Text = "Canceling scan...";
            }
        }

        private void RenderAlbumResults(List<SimilarGroup> groups) {
            _resultsContentPanel.Children.Clear();

            int totalSimilarPhotos = groups.Sum(g => g.Photos.Count);
            long totalBytes = groups.Sum(g => g.Photos.Sum(p => p.FileSizeBytes));

            // Calculate potential space savings (all photos except the 1 best in each group)
            long potentialSavingsBytes = groups.Sum(g => g.Photos.Skip(1).Sum(p => p.FileSizeBytes));

            // Update Header Subtitle (Exact Vivo style!)
            _headerSubtitle.Text = string.Format("{0} similar photos in total, using {1} (reclaimable: ~{2})",
                totalSimilarPhotos, ImageItem.FormatFileSize(totalBytes), ImageItem.FormatFileSize(potentialSavingsBytes));

            if (groups.Count == 0) {
                var noMatches = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 50, 0, 0) };
                noMatches.Children.Add(new TextBlock {
                    Text = "✅ No duplicate or similar photos found!",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                noMatches.Children.Add(new TextBlock {
                    Text = "Your album is clean. If you want to detect more variations, try lowering the Similarity Sensitivity slider.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _resultsContentPanel.Children.Add(noMatches);
                _toolbarBorder.Visibility = Visibility.Collapsed;
                _bottomFloatingBar.Visibility = Visibility.Collapsed;
                return;
            }

            _toolbarBorder.Visibility = Visibility.Visible;
            _bottomFloatingBar.Visibility = Visibility.Visible;
            _resultsStatsText.Text = string.Format("Found {0} duplicate/similar groups ({1} photos)", groups.Count, totalSimilarPhotos);

            // Render each group
            foreach (var group in groups) {
                var groupCard = CreateGroupContainer(group);
                _resultsContentPanel.Children.Add(groupCard);
            }

            UpdateSelectionSummary();
        }

        private Border CreateGroupContainer(SimilarGroup group) {
            var groupBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(17, 19, 25)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 36, 48)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(16, 12, 16, 14)
            };
            var groupStack = new StackPanel();

            // Group Header Row (Vivo Style: Checkbox + Date + Match badge)
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };

            var badgeBorder = new Border {
                Background = new SolidColorBrush(group.MaxSimilarity >= 99 ? Color.FromRgb(16, 185, 129) : Color.FromRgb(59, 130, 246)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = new TextBlock {
                Text = group.SimilarityBadge,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            DockPanel.SetDock(badgeBorder, Dock.Right);
            headerDock.Children.Add(badgeBorder);

            var groupCheck = new CheckBox {
                IsChecked = group.IsGroupSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            groupCheck.Click += (s, e) => {
                bool isChecked = groupCheck.IsChecked == true;
                foreach (var p in group.Photos) {
                    p.IsSelected = isChecked;
                }
                UpdateSelectionSummary();
            };
            DockPanel.SetDock(groupCheck, Dock.Left);
            headerDock.Children.Add(groupCheck);

            var dateText = new TextBlock {
                Text = group.DateHeader,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerDock.Children.Add(dateText);
            groupStack.Children.Add(headerDock);

            // Photos Row (Side-by-side)
            var photosGrid = new Grid();
            int count = group.Photos.Count;
            for (int i = 0; i < count; i++) {
                photosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (i < count - 1) {
                    photosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); // Gap
                }
            }

            for (int i = 0; i < count; i++) {
                var photo = group.Photos[i];
                var photoCard = CreatePhotoCard(photo, group, i == 0 && count > 1 ? "★ Best Quality" : null);
                Grid.SetColumn(photoCard, i * 2);
                photosGrid.Children.Add(photoCard);
            }

            groupStack.Children.Add(photosGrid);
            groupBorder.Child = groupStack;
            return groupBorder;
        }

        private Border CreatePhotoCard(ImageItem photo, SimilarGroup parentGroup, string qualityTag) {
            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(22, 25, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 46, 62)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true
            };
            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) }); // Image thumbnail
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Metadata & Buttons

            // Thumbnail Image Area
            var imgContainer = new Grid { Background = new SolidColorBrush(Color.FromRgb(10, 11, 15)) };
            try {
                var img = new Image {
                    Source = photo.Thumbnail,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                imgContainer.Children.Add(img);
            } catch { }

            // Quality Tag (e.g. Best Quality / Highest Res)
            if (!string.IsNullOrEmpty(qualityTag)) {
                var tagBorder = new Border {
                    Background = new SolidColorBrush(Color.FromArgb(200, 16, 185, 129)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(8)
                };
                tagBorder.Child = new TextBlock {
                    Text = qualityTag,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                imgContainer.Children.Add(tagBorder);
            }

            // Top-right Checkbox (Vivo Style)
            var checkBorder = new Border {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                CornerRadius = new CornerRadius(14),
                Width = 28,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8)
            };
            var photoCheck = new CheckBox {
                IsChecked = photo.IsSelected,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            photoCheck.Click += (s, e) => {
                photo.IsSelected = photoCheck.IsChecked == true;
                UpdateSelectionSummary();
            };
            checkBorder.Child = photoCheck;
            imgContainer.Children.Add(checkBorder);

            // Bottom-right Expand / Zoom Button (Vivo Style ⤢)
            var expandBtn = new Button {
                Content = "⤢",
                Width = 28,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Compare Side-by-Side in Full Screen"
            };
            expandBtn.Click += (s, e) => {
                // Find comparison partner
                ImageItem partner = parentGroup.Photos.FirstOrDefault(p => p != photo) ?? photo;
                OpenCompareWindow(photo, partner, parentGroup.MaxSimilarity);
            };
            imgContainer.Children.Add(expandBtn);

            Grid.SetRow(imgContainer, 0);
            cardGrid.Children.Add(imgContainer);

            // Metadata Area
            var metaStack = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            var nameText = new TextBlock {
                Text = photo.FileName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            metaStack.Children.Add(nameText);

            var detailsText = new TextBlock {
                Text = string.Format("📅 {0}  •  {1}\n📐 {2}  •  💾 {3}",
                    photo.TimeFormatted, photo.DateFormatted, photo.ResolutionFormatted, photo.FileSizeFormatted),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                Margin = new Thickness(0, 4, 0, 6)
            };
            metaStack.Children.Add(detailsText);

            // Individual Actions Row
            var actionRow = new DockPanel();
            var openExplorerBtn = new Button {
                Content = "Open Folder",
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(35, 40, 52)),
                Foreground = Brushes.White,
                FontSize = 10,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            openExplorerBtn.Click += (s, e) => {
                try {
                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", photo.FilePath));
                } catch { }
            };
            DockPanel.SetDock(openExplorerBtn, Dock.Left);
            actionRow.Children.Add(openExplorerBtn);

            var deleteSingleBtn = new Button {
                Content = "🗑 Delete",
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                Foreground = Brushes.White,
                FontSize = 10,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            deleteSingleBtn.Click += (s, e) => DeleteSinglePhoto(photo, parentGroup);
            actionRow.Children.Add(deleteSingleBtn);

            metaStack.Children.Add(actionRow);

            Grid.SetRow(metaStack, 1);
            cardGrid.Children.Add(metaStack);

            card.Child = cardGrid;
            return card;
        }

        private void RenderReferenceResults(List<ReferenceMatchItem> matches) {
            _resultsContentPanel.Children.Clear();

            _headerSubtitle.Text = string.Format("Found {0} matching photos in target album", matches.Count);

            if (matches.Count == 0) {
                var noMatches = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 50, 0, 0) };
                noMatches.Children.Add(new TextBlock {
                    Text = "🔍 No matching or similar photos found in this album",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                noMatches.Children.Add(new TextBlock {
                    Text = "Try lowering the similarity threshold or check that the target folder contains images.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _resultsContentPanel.Children.Add(noMatches);
                _toolbarBorder.Visibility = Visibility.Collapsed;
                _bottomFloatingBar.Visibility = Visibility.Collapsed;
                return;
            }

            _toolbarBorder.Visibility = Visibility.Visible;
            _bottomFloatingBar.Visibility = Visibility.Visible;
            _resultsStatsText.Text = string.Format("Found {0} matching photos", matches.Count);

            foreach (var match in matches) {
                var matchCard = CreateReferenceMatchCard(match);
                _resultsContentPanel.Children.Add(matchCard);
            }

            UpdateSelectionSummary();
        }

        private Border CreateReferenceMatchCard(ReferenceMatchItem match) {
            var border = new Border {
                Background = new SolidColorBrush(Color.FromRgb(17, 19, 25)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(32, 36, 48)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 14),
                Padding = new Thickness(16, 12, 16, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Ref
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // Match info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Target

            // Left: Reference Photo
            var refBox = CreateMiniPreview(match.ReferencePhoto, "Target Reference Photo");
            Grid.SetColumn(refBox, 0);
            grid.Children.Add(refBox);

            // Center: Match Info & Buttons
            var centerStack = new StackPanel {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            var badge = new Border {
                Background = new SolidColorBrush(match.Similarity >= 98 ? Color.FromRgb(16, 185, 129) : Color.FromRgb(59, 130, 246)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            badge.Child = new TextBlock {
                Text = match.SimilarityBadge,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            centerStack.Children.Add(badge);

            var compareBtn = new Button {
                Content = "⤢ Compare",
                Height = 28,
                Padding = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(40, 46, 62)),
                Foreground = Brushes.White,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            compareBtn.Click += (s, e) => OpenCompareWindow(match.ReferencePhoto, match.MatchedPhoto, match.Similarity);
            centerStack.Children.Add(compareBtn);

            Grid.SetColumn(centerStack, 1);
            grid.Children.Add(centerStack);

            // Right: Matched Photo
            var targetBox = CreateMiniPreview(match.MatchedPhoto, "Match Found in Album", match);
            Grid.SetColumn(targetBox, 2);
            grid.Children.Add(targetBox);

            border.Child = grid;
            return border;
        }

        private Border CreateMiniPreview(ImageItem photo, string label, ReferenceMatchItem matchItem = null) {
            var border = new Border {
                Background = new SolidColorBrush(Color.FromRgb(22, 25, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 46, 62)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel();

            var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            if (matchItem != null) {
                var check = new CheckBox {
                    IsChecked = matchItem.IsSelected,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                check.Click += (s, e) => {
                    matchItem.IsSelected = check.IsChecked == true;
                    UpdateSelectionSummary();
                };
                DockPanel.SetDock(check, Dock.Left);
                topRow.Children.Add(check);
            }

            var labelText = new TextBlock {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 186, 202))
            };
            topRow.Children.Add(labelText);
            stack.Children.Add(topRow);

            var imgContainer = new Border {
                Background = new SolidColorBrush(Color.FromRgb(10, 11, 15)),
                CornerRadius = new CornerRadius(6),
                Height = 160,
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 6)
            };
            try {
                var img = new Image {
                    Source = photo.Thumbnail,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                imgContainer.Child = img;
            } catch { }
            stack.Children.Add(imgContainer);

            var nameText = new TextBlock {
                Text = photo.FileName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            stack.Children.Add(nameText);

            var detailsText = new TextBlock {
                Text = string.Format("📅 {0}  •  📐 {1}  •  💾 {2}",
                    photo.DateFormatted, photo.ResolutionFormatted, photo.FileSizeFormatted),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(detailsText);

            border.Child = stack;
            return border;
        }

        private void OpenCompareWindow(ImageItem photoA, ImageItem photoB, double similarity) {
            bool sendToRecycle = _recycleBinCheck.IsChecked == true;
            var win = new CompareWindow(photoA, photoB, similarity, sendToRecycle);
            win.Owner = this;
            win.OnPhotoDeleted += deletedPath => {
                RemoveDeletedPhotoFromUI(deletedPath);
            };
            win.ShowDialog();
        }

        private void AutoSelectDuplicates() {
            if (_currentMode == 0) {
                foreach (var g in _albumGroups) {
                    for (int i = 0; i < g.Photos.Count; i++) {
                        // Keep the 1st one (highest resolution/best quality), check the rest
                        g.Photos[i].IsSelected = (i > 0);
                    }
                    g.IsGroupSelected = false;
                }
            } else {
                foreach (var m in _referenceMatches) {
                    m.IsSelected = true;
                }
            }
            RefreshSelectionUI();
        }

        private void SelectDuplicatesExceptFirst() {
            AutoSelectDuplicates();
        }

        private void DeselectAll() {
            if (_currentMode == 0) {
                foreach (var g in _albumGroups) {
                    foreach (var p in g.Photos) p.IsSelected = false;
                    g.IsGroupSelected = false;
                }
            } else {
                foreach (var m in _referenceMatches) m.IsSelected = false;
            }
            RefreshSelectionUI();
        }

        private void RefreshSelectionUI() {
            // Re-render UI to update checkbox states
            if (_currentMode == 0) {
                RenderAlbumResults(_albumGroups);
            } else {
                RenderReferenceResults(_referenceMatches);
            }
        }

        private void UpdateSelectionSummary() {
            int selectedCount = 0;
            long selectedBytes = 0;

            if (_currentMode == 0) {
                foreach (var g in _albumGroups) {
                    foreach (var p in g.Photos) {
                        if (p.IsSelected) {
                            selectedCount++;
                            selectedBytes += p.FileSizeBytes;
                        }
                    }
                }
            } else {
                foreach (var m in _referenceMatches) {
                    if (m.IsSelected) {
                        selectedCount++;
                        selectedBytes += m.MatchedPhoto.FileSizeBytes;
                    }
                }
            }

            _selectedSummaryText.Text = string.Format("{0} photos selected ({1} to free)",
                selectedCount, ImageItem.FormatFileSize(selectedBytes));

            if (selectedCount > 0) {
                _deleteSelectedBtn.IsEnabled = true;
                _deleteSelectedBtn.Content = string.Format("Delete ({0} item{1}, {2})",
                    selectedCount, selectedCount == 1 ? "" : "s", ImageItem.FormatFileSize(selectedBytes));
                _deleteSelectedBtn.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            } else {
                _deleteSelectedBtn.IsEnabled = false;
                _deleteSelectedBtn.Content = "Delete";
                _deleteSelectedBtn.Background = new SolidColorBrush(Color.FromRgb(45, 52, 68));
            }
        }

        private void DeleteSelectedItems() {
            var toDelete = new List<ImageItem>();
            if (_currentMode == 0) {
                foreach (var g in _albumGroups) {
                    toDelete.AddRange(g.Photos.Where(p => p.IsSelected));
                }
            } else {
                toDelete.AddRange(_referenceMatches.Where(m => m.IsSelected).Select(m => m.MatchedPhoto));
            }

            if (toDelete.Count == 0) return;

            bool sendToRecycle = _recycleBinCheck.IsChecked == true;
            long totalBytes = toDelete.Sum(p => p.FileSizeBytes);

            var ans = MessageBox.Show(this,
                string.Format("Are you sure you want to delete {0} photo(s)?\n\nTotal space to free: {1}\nDestination: {2}",
                    toDelete.Count,
                    ImageItem.FormatFileSize(totalBytes),
                    sendToRecycle ? "Windows Recycle Bin (Restorable)" : "Permanently Deleted"),
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (ans != MessageBoxResult.Yes) return;

            int deletedCount = 0;
            var failedFiles = new List<string>();

            foreach (var item in toDelete) {
                string err;
                if (FileOperations.DeleteFile(item.FilePath, sendToRecycle, out err)) {
                    deletedCount++;
                    RemoveDeletedPhotoFromUI(item.FilePath);
                } else {
                    failedFiles.Add(Path.GetFileName(item.FilePath) + ": " + err);
                }
            }

            if (failedFiles.Count > 0) {
                MessageBox.Show(this, "Could not delete some files:\n" + string.Join("\n", failedFiles.Take(5)), "Delete Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
            } else {
                MessageBox.Show(this, string.Format("Successfully deleted {0} photo(s)!\nFreed {1}.", deletedCount, ImageItem.FormatFileSize(totalBytes)),
                    "Cleanup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            UpdateSelectionSummary();
        }

        private void DeleteSinglePhoto(ImageItem photo, SimilarGroup parentGroup) {
            bool sendToRecycle = _recycleBinCheck.IsChecked == true;
            var ans = MessageBox.Show(this,
                string.Format("Delete photo:\n{0}\n\n(Recycle Bin: {1})", photo.FilePath, sendToRecycle ? "Yes" : "No"),
                "Delete Photo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (ans == MessageBoxResult.Yes) {
                string err;
                if (FileOperations.DeleteFile(photo.FilePath, sendToRecycle, out err)) {
                    RemoveDeletedPhotoFromUI(photo.FilePath);
                } else {
                    MessageBox.Show(this, "Failed to delete: " + err, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveDeletedPhotoFromUI(string filePath) {
            if (_currentMode == 0) {
                foreach (var g in _albumGroups.ToList()) {
                    var match = g.Photos.FirstOrDefault(p => string.Equals(p.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                    if (match != null) {
                        g.Photos.Remove(match);
                        if (g.Photos.Count < 2) {
                            _albumGroups.Remove(g);
                        }
                    }
                }
                RenderAlbumResults(_albumGroups);
            } else {
                _referenceMatches.RemoveAll(m => string.Equals(m.MatchedPhoto.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                RenderReferenceResults(_referenceMatches);
            }
        }

        private void ResetUI() {
            _albumGroups.Clear();
            _referenceMatches.Clear();
            _referenceFiles.Clear();
            RefreshReferenceChips();
            _toolbarBorder.Visibility = Visibility.Collapsed;
            _bottomFloatingBar.Visibility = Visibility.Collapsed;
            _headerTitle.Text = "‹ Duplicates by S1mplee67";
            _headerSubtitle.Text = "Offline Visual Duplicate & Similar Photo Cleaner • Created by S1mplee67";
            ShowWelcomeEmptyState();
        }

        private void ShowAboutDialog() {
            var aboutWin = new Window {
                Title = "About Duplicates by S1mplee67",
                Width = 470,
                Height = 460,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(14, 17, 24)),
                Foreground = Brushes.White
            };

            var stack = new StackPanel { Margin = new Thickness(24) };

            var iconText = new TextBlock {
                Text = "📸",
                FontSize = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(iconText);

            var titleText = new TextBlock {
                Text = "Duplicates by S1mplee67",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(titleText);

            var verText = new TextBlock {
                Text = "Version 1.0.0  •  MIT License",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 156, 172)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(verText);

            var descBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(20, 24, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 45, 62)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var descStack = new StackPanel();
            descStack.Children.Add(new TextBlock {
                Text = "High-performance offline duplicate and similar photo cleaner that analyzes visual pixel patterns, gradients, and color distribution.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 205, 220)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 8)
            });
            descStack.Children.Add(new TextBlock {
                Text = "• Algorithm: 128-bit dHash + 64-bit aHash + 16-Cell RGB + MD5\n• Modes: Whole Album Scanner & Reference Photo Matcher\n• Privacy: 100% Offline, zero tracking or network usage\n• Safety: Native Windows Recycle Bin support",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)),
                LineHeight = 17
            });
            descBorder.Child = descStack;
            stack.Children.Add(descBorder);

            var authorText = new TextBlock {
                Text = "Created with ❤️ by S1mplee67",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(authorText);

            var okBtn = new Button {
                Content = "Close",
                Width = 90,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okBtn.Click += (s, e) => aboutWin.Close();
            stack.Children.Add(okBtn);

            aboutWin.Content = stack;
            aboutWin.ShowDialog();
        }
    }
}
