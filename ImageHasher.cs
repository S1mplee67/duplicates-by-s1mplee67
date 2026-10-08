using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SimilarPhotoFinder {
    public static class ImageHasher {
        public static ImageItem ExtractImageInfo(string filePath) {
            var fi = new FileInfo(filePath);
            if (!fi.Exists) return null;

            var item = new ImageItem {
                FilePath = fi.FullName,
                FileName = fi.Name,
                FileSizeBytes = fi.Length,
                FileSizeFormatted = ImageItem.FormatFileSize(fi.Length)
            };

            // Calculate MD5 for exact duplicate check
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                byte[] hash = md5.ComputeHash(stream);
                item.MD5Hash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            // Load image using GDI+
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var original = Image.FromStream(stream, false, false)) {
                item.Width = original.Width;
                item.Height = original.Height;
                item.ResolutionFormatted = string.Format("{0} × {1}", item.Width, item.Height);
                item.AspectRatio = (double)item.Width / Math.Max(1, item.Height);

                // Try to read EXIF date
                item.DateTaken = GetExifDate(original, fi.LastWriteTime);
                item.DateFormatted = item.DateTaken.ToString("MMMM d, yyyy");
                item.DateGroupKey = item.DateTaken.ToString("yyyy-MM-dd");
                item.TimeFormatted = item.DateTaken.ToString("h:mm tt");

                // Compute dHash Horizontal (9x8)
                item.DHash_H = ComputeDHash(original, 9, 8, true);

                // Compute dHash Vertical (8x9)
                item.DHash_V = ComputeDHash(original, 8, 9, false);

                // Compute aHash (8x8)
                item.AHash = ComputeAHash(original, 8, 8);

                // Compute Color Grid (4x4 = 16 RGB values)
                item.ColorGrid = ComputeColorGrid(original, 4, 4);
            }

            return item;
        }

        private static DateTime GetExifDate(Image img, DateTime fallback) {
            try {
                // 0x9003 = ExifDTOrig, 0x0132 = DateTime
                int[] dateTags = new int[] { 0x9003, 0x0132, 0x9004 };
                foreach (int tag in dateTags) {
                    try {
                        var prop = img.GetPropertyItem(tag);
                        if (prop != null && prop.Value != null) {
                            string dateStr = Encoding.ASCII.GetString(prop.Value).Trim('\0', ' ', '\r', '\n');
                            DateTime dt;
                            if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {
                                return dt;
                            }
                        }
                    } catch { }
                }
            } catch { }
            return fallback;
        }

        private static ulong ComputeDHash(Image original, int w, int h, bool horizontal) {
            using (var small = new Bitmap(w, h, PixelFormat.Format24bppRgb)) {
                using (var g = Graphics.FromImage(small)) {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(original, 0, 0, w, h);
                }

                int[,] gray = new int[w, h];
                for (int y = 0; y < h; y++) {
                    for (int x = 0; x < w; x++) {
                        Color c = small.GetPixel(x, y);
                        gray[x, y] = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                    }
                }

                ulong hash = 0;
                int bit = 0;
                if (horizontal) {
                    for (int y = 0; y < 8; y++) {
                        for (int x = 0; x < 8; x++) {
                            if (gray[x, y] > gray[x + 1, y]) {
                                hash |= (1UL << bit);
                            }
                            bit++;
                        }
                    }
                } else {
                    for (int y = 0; y < 8; y++) {
                        for (int x = 0; x < 8; x++) {
                            if (gray[x, y] > gray[x, y + 1]) {
                                hash |= (1UL << bit);
                            }
                            bit++;
                        }
                    }
                }
                return hash;
            }
        }

        private static ulong ComputeAHash(Image original, int w, int h) {
            using (var small = new Bitmap(w, h, PixelFormat.Format24bppRgb)) {
                using (var g = Graphics.FromImage(small)) {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(original, 0, 0, w, h);
                }

                int[] gray = new int[w * h];
                int sum = 0;
                int idx = 0;
                for (int y = 0; y < h; y++) {
                    for (int x = 0; x < w; x++) {
                        Color c = small.GetPixel(x, y);
                        int val = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                        gray[idx++] = val;
                        sum += val;
                    }
                }

                int avg = sum / (w * h);
                ulong hash = 0;
                for (int i = 0; i < 64 && i < gray.Length; i++) {
                    if (gray[i] >= avg) {
                        hash |= (1UL << i);
                    }
                }
                return hash;
            }
        }

        private static byte[] ComputeColorGrid(Image original, int w, int h) {
            byte[] grid = new byte[w * h * 3];
            using (var small = new Bitmap(w, h, PixelFormat.Format24bppRgb)) {
                using (var g = Graphics.FromImage(small)) {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(original, 0, 0, w, h);
                }

                int idx = 0;
                for (int y = 0; y < h; y++) {
                    for (int x = 0; x < w; x++) {
                        Color c = small.GetPixel(x, y);
                        grid[idx++] = c.R;
                        grid[idx++] = c.G;
                        grid[idx++] = c.B;
                    }
                }
            }
            return grid;
        }

        public static int HammingDistance(ulong h1, ulong h2) {
            ulong x = h1 ^ h2;
            int dist = 0;
            while (x != 0) {
                dist += (int)(x & 1);
                x >>= 1;
            }
            return dist;
        }

        public static double CalculateSimilarity(ImageItem a, ImageItem b) {
            if (a == null || b == null) return 0.0;
            if (string.Equals(a.MD5Hash, b.MD5Hash, StringComparison.OrdinalIgnoreCase)) {
                return 100.0; // Bit-for-bit identical
            }

            int distH = HammingDistance(a.DHash_H, b.DHash_H);
            int distV = HammingDistance(a.DHash_V, b.DHash_V);
            int distA = HammingDistance(a.AHash, b.AHash);

            int totalDist = distH + distV + distA; // Range: 0 to 192

            // Scale structural similarity: a distance of 0 gives 1.0; 60 or more gives 0.0
            double structSim = Math.Max(0.0, 1.0 - (double)totalDist / 60.0);

            // Color difference (Euclidean distance across 16 RGB cells)
            double colorDiff = 0.0;
            if (a.ColorGrid != null && b.ColorGrid != null && a.ColorGrid.Length == b.ColorGrid.Length) {
                double diffSum = 0.0;
                for (int i = 0; i < a.ColorGrid.Length; i += 3) {
                    double dr = a.ColorGrid[i] - b.ColorGrid[i];
                    double dg = a.ColorGrid[i + 1] - b.ColorGrid[i + 1];
                    double db = a.ColorGrid[i + 2] - b.ColorGrid[i + 2];
                    diffSum += Math.Sqrt(dr * dr + dg * dg + db * db);
                }
                // Max distance for 16 cells is 16 * sqrt(255^2 * 3) ~ 7066.7
                colorDiff = diffSum / 7066.7;
            }
            double colorSim = Math.Max(0.0, 1.0 - colorDiff);

            // Aspect ratio check: if one is landscape and one is portrait, penalize unless structSim is very high
            double ratioDiff = Math.Abs(a.AspectRatio - b.AspectRatio);
            double aspectFactor = 1.0;
            if (ratioDiff > 0.4 && structSim < 0.95) {
                aspectFactor = 0.85;
            }

            double combined = (structSim * 0.70 + colorSim * 0.30) * aspectFactor * 100.0;
            return Math.Round(Math.Min(100.0, Math.Max(0.0, combined)), 1);
        }
    }
}
