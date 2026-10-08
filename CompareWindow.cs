using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimilarPhotoFinder {
    public class CompareWindow : Window {
        private ImageItem _photoA;
        private ImageItem _photoB;
        private double _similarity;
        private bool _sendToRecycleBin;
        public event Action<string> OnPhotoDeleted;

        public CompareWindow(ImageItem photoA, ImageItem photoB, double similarity, bool sendToRecycleBin) {
            _photoA = photoA;
            _photoB = photoB;
            _similarity = similarity;
            _sendToRecycleBin = sendToRecycleBin;

            Title = string.Format("Compare Photos - {0:F0}% Match • Duplicates by S1mplee67", similarity);
            Width = 960;
            Height = 700;
            MinWidth = 800;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(15, 17, 23));

            BuildUI();
        }

        private void BuildUI() {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Images
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Metadata
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Actions

            // Header
            var headerBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(22, 25, 35)),
                Padding = new Thickness(20, 14, 20, 14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 43, 56)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerPanel = new DockPanel();
            var titleText = new TextBlock {
                Text = "Side-by-Side Comparison • Duplicates by S1mplee67",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            var simBadge = new Border {
                Background = new SolidColorBrush(_similarity >= 95 ? Color.FromRgb(16, 185, 129) : Color.FromRgb(59, 130, 246)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            simBadge.Child = new TextBlock {
                Text = _similarity >= 99.5 ? "Exact Duplicate (100%)" : string.Format("{0:F1}% Pixel Match", _similarity),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            DockPanel.SetDock(simBadge, Dock.Right);
            headerPanel.Children.Add(simBadge);
            headerPanel.Children.Add(titleText);
            headerBorder.Child = headerPanel;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // Images Side-by-Side
            var imagesGrid = new Grid { Margin = new Thickness(16, 12, 16, 8) };
            imagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            imagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Spacing
            imagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var panelA = CreateImagePreviewBox(_photoA, "Photo 1 (Left)");
            var panelB = CreateImagePreviewBox(_photoB, "Photo 2 (Right)");

            Grid.SetColumn(panelA, 0);
            Grid.SetColumn(panelB, 2);
            imagesGrid.Children.Add(panelA);
            imagesGrid.Children.Add(panelB);

            Grid.SetRow(imagesGrid, 1);
            rootGrid.Children.Add(imagesGrid);

            // Metadata Comparison Row
            var metaBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(20, 23, 31)),
                Margin = new Thickness(16, 0, 16, 12),
                Padding = new Thickness(16, 10, 16, 10),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 40, 54)),
                BorderThickness = new Thickness(1)
            };
            var metaGrid = new Grid();
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var metaStackA = CreateMetadataStack(_photoA);
            var metaStackB = CreateMetadataStack(_photoB);
            Grid.SetColumn(metaStackA, 0);
            Grid.SetColumn(metaStackB, 2);
            metaGrid.Children.Add(metaStackA);
            metaGrid.Children.Add(metaStackB);
            metaBorder.Child = metaGrid;

            Grid.SetRow(metaBorder, 2);
            rootGrid.Children.Add(metaBorder);

            // Footer Actions
            var footerBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(22, 25, 35)),
                Padding = new Thickness(16, 12, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 43, 56)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerPanel = new DockPanel();

            var closeBtn = new Button {
                Content = "Close",
                Width = 90,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(45, 52, 68)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => Close();
            DockPanel.SetDock(closeBtn, Dock.Right);
            footerPanel.Children.Add(closeBtn);

            var actionsStack = new StackPanel { Orientation = Orientation.Horizontal };

            var keepLeftBtn = CreateActionButton("Keep Left (Delete Right)", Color.FromRgb(220, 38, 38), () => {
                DeleteSpecific(_photoB);
            });
            var keepRightBtn = CreateActionButton("Keep Right (Delete Left)", Color.FromRgb(220, 38, 38), () => {
                DeleteSpecific(_photoA);
            });
            var deleteBothBtn = CreateActionButton("Delete Both", Color.FromRgb(153, 27, 27), () => {
                DeleteBoth();
            });

            actionsStack.Children.Add(keepLeftBtn);
            actionsStack.Children.Add(keepRightBtn);
            actionsStack.Children.Add(deleteBothBtn);

            footerPanel.Children.Add(actionsStack);
            footerBorder.Child = footerPanel;

            Grid.SetRow(footerBorder, 3);
            rootGrid.Children.Add(footerBorder);

            Content = rootGrid;
        }

        private Border CreateImagePreviewBox(ImageItem item, string label) {
            var border = new Border {
                Background = new SolidColorBrush(Color.FromRgb(10, 11, 15)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 43, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true
            };

            var grid = new Grid();
            if (File.Exists(item.FilePath)) {
                try {
                    var img = new Image {
                        Source = ImageItem.LoadThumbnail(item.FilePath, 1000),
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    grid.Children.Add(img);
                } catch { }
            }

            var labelBorder = new Border {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8)
            };
            labelBorder.Child = new TextBlock {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            grid.Children.Add(labelBorder);

            border.Child = grid;
            return border;
        }

        private StackPanel CreateMetadataStack(ImageItem item) {
            var sp = new StackPanel { Margin = new Thickness(4) };

            var nameRow = new TextBlock {
                Text = item.FileName,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            sp.Children.Add(nameRow);

            var pathRow = new TextBlock {
                Text = item.FilePath,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 146, 160)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = Cursors.Hand,
                ToolTip = "Click to open folder in Windows Explorer"
            };
            pathRow.MouseLeftButtonDown += (s, e) => {
                try {
                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", item.FilePath));
                } catch { }
            };
            sp.Children.Add(pathRow);

            var detailsRow = new TextBlock {
                Text = string.Format("📅 {0} {1}   •   📐 {2}   •   💾 {3}",
                    item.DateFormatted, item.TimeFormatted, item.ResolutionFormatted, item.FileSizeFormatted),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 186, 200)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            sp.Children.Add(detailsRow);

            return sp;
        }

        private Button CreateActionButton(string text, Color bg, Action onClick) {
            var btn = new Button {
                Content = text,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush(bg),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void DeleteSpecific(ImageItem target) {
            var ans = MessageBox.Show(this,
                string.Format("Are you sure you want to delete:\n\n{0}\n\n(It will be sent to Recycle Bin: {1})",
                    target.FilePath, _sendToRecycleBin ? "Yes" : "No (Permanent)"),
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (ans == MessageBoxResult.Yes) {
                string err;
                if (FileOperations.DeleteFile(target.FilePath, _sendToRecycleBin, out err)) {
                    if (OnPhotoDeleted != null) OnPhotoDeleted(target.FilePath);
                    Close();
                } else {
                    MessageBox.Show(this, "Failed to delete file:\n" + err, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteBoth() {
            var ans = MessageBox.Show(this,
                string.Format("Are you sure you want to delete BOTH photos?\n\n1: {0}\n2: {1}\n\n(Recycle Bin: {2})",
                    _photoA.FilePath, _photoB.FilePath, _sendToRecycleBin ? "Yes" : "No"),
                "Confirm Delete Both",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (ans == MessageBoxResult.Yes) {
                string err;
                bool okA = FileOperations.DeleteFile(_photoA.FilePath, _sendToRecycleBin, out err);
                if (okA && OnPhotoDeleted != null) OnPhotoDeleted(_photoA.FilePath);

                bool okB = FileOperations.DeleteFile(_photoB.FilePath, _sendToRecycleBin, out err);
                if (okB && OnPhotoDeleted != null) OnPhotoDeleted(_photoB.FilePath);

                Close();
            }
        }
    }
}
