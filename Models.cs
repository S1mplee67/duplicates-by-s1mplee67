using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace SimilarPhotoFinder {
    public class ImageItem : INotifyPropertyChanged {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ResolutionFormatted { get; set; }
        public DateTime DateTaken { get; set; }
        public string DateFormatted { get; set; }
        public string DateGroupKey { get; set; }
        public string TimeFormatted { get; set; }

        public string MD5Hash { get; set; }
        public ulong DHash_H { get; set; }
        public ulong DHash_V { get; set; }
        public ulong AHash { get; set; }
        public byte[] ColorGrid { get; set; }
        public double AspectRatio { get; set; }

        private bool _isSelected;
        public bool IsSelected {
            get { return _isSelected; }
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private BitmapSource _thumbnail;
        public BitmapSource Thumbnail {
            get {
                if (_thumbnail == null && File.Exists(FilePath)) {
                    try {
                        _thumbnail = LoadThumbnail(FilePath, 320);
                    } catch { }
                }
                return _thumbnail;
            }
        }

        public static BitmapSource LoadThumbnail(string path, int decodeWidth = 320) {
            try {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.UriSource = new Uri(Path.GetFullPath(path));
                if (decodeWidth > 0) {
                    bi.DecodePixelWidth = decodeWidth;
                }
                bi.EndInit();
                bi.Freeze();
                return bi;
            } catch {
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }

        public static string FormatFileSize(long bytes) {
            if (bytes >= 1024 * 1024 * 1024)
                return string.Format("{0:F2} GB", bytes / (1024.0 * 1024 * 1024));
            if (bytes >= 1024 * 1024)
                return string.Format("{0:F2} MB", bytes / (1024.0 * 1024));
            if (bytes >= 1024)
                return string.Format("{0:F1} KB", bytes / 1024.0);
            return bytes + " B";
        }
    }

    public class SimilarGroup : INotifyPropertyChanged {
        public string GroupId { get; set; }
        public string DateHeader { get; set; }
        public double MaxSimilarity { get; set; }
        public string SimilarityBadge { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<ImageItem> Photos { get; set; }

        private bool? _isGroupSelected = false;
        public bool? IsGroupSelected {
            get { return _isGroupSelected; }
            set {
                if (_isGroupSelected != value) {
                    _isGroupSelected = value;
                    OnPropertyChanged("IsGroupSelected");
                }
            }
        }

        public SimilarGroup() {
            Photos = new System.Collections.ObjectModel.ObservableCollection<ImageItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ReferenceMatchItem : INotifyPropertyChanged {
        public ImageItem ReferencePhoto { get; set; }
        public ImageItem MatchedPhoto { get; set; }
        public double Similarity { get; set; }
        public string SimilarityBadge { get; set; }

        private bool _isSelected;
        public bool IsSelected {
            get { return _isSelected; }
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    MatchedPhoto.IsSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
