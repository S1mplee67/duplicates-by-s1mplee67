using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimilarPhotoFinder {
    public class ScanProgressInfo {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentFileName { get; set; }
        public string StatusMessage { get; set; }
        public double Percentage {
            get { return Total > 0 ? ((double)Current / Total) * 100.0 : 0.0; }
        }
    }

    public static class Scanner {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff"
        };

        public static List<string> GetImageFiles(string folderPath, bool recursive, CancellationToken token) {
            var files = new List<string>();
            if (!Directory.Exists(folderPath)) return files;

            try {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.EnumerateFiles(folderPath, "*.*", searchOption);
                foreach (var f in allFiles) {
                    if (token.IsCancellationRequested) break;
                    string ext = Path.GetExtension(f);
                    if (!string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext)) {
                        files.Add(f);
                    }
                }
            } catch (Exception) {
                // If recursive failed due to permissions in some subfolder, do recursive manual walk
                try {
                    WalkDirectory(folderPath, recursive, files, token);
                } catch { }
            }
            return files;
        }

        private static void WalkDirectory(string dir, bool recursive, List<string> result, CancellationToken token) {
            if (token.IsCancellationRequested) return;
            try {
                foreach (var f in Directory.GetFiles(dir)) {
                    if (token.IsCancellationRequested) return;
                    string ext = Path.GetExtension(f);
                    if (!string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext)) {
                        result.Add(f);
                    }
                }
                if (recursive) {
                    foreach (var sub in Directory.GetDirectories(dir)) {
                        if (token.IsCancellationRequested) return;
                        WalkDirectory(sub, true, result, token);
                    }
                }
            } catch { }
        }

        public static async Task<List<SimilarGroup>> ScanAlbumAsync(
            string folderPath,
            bool recursive,
            double similarityThreshold,
            Action<ScanProgressInfo> progressCallback,
            CancellationToken token) {

            return await Task.Run(() => {
                var files = GetImageFiles(folderPath, recursive, token);
                if (files.Count == 0 || token.IsCancellationRequested) {
                    return new List<SimilarGroup>();
                }

                // Step 1: Extract features in parallel
                var items = new List<ImageItem>();
                int processed = 0;
                int total = files.Count;
                object syncLock = new object();

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (file, state) => {
                    if (token.IsCancellationRequested) {
                        state.Stop();
                        return;
                    }

                    try {
                        var info = ImageHasher.ExtractImageInfo(file);
                        if (info != null) {
                            lock (syncLock) {
                                items.Add(info);
                            }
                        }
                    } catch { }

                    int current = Interlocked.Increment(ref processed);
                    if (progressCallback != null && (current % 5 == 0 || current == total)) {
                        progressCallback(new ScanProgressInfo {
                            Current = current,
                            Total = total,
                            CurrentFileName = Path.GetFileName(file),
                            StatusMessage = string.Format("Analyzing photo {0} of {1}...", current, total)
                        });
                    }
                });

                if (token.IsCancellationRequested || items.Count < 2) {
                    return new List<SimilarGroup>();
                }

                if (progressCallback != null) {
                    progressCallback(new ScanProgressInfo {
                        Current = total,
                        Total = total,
                        StatusMessage = "Comparing pixels and clustering similar photos..."
                    });
                }

                // Step 2: Compare pairs
                int n = items.Count;
                var dsu = new DisjointSet(n);
                var pairSimilarities = new Dictionary<string, double>();

                for (int i = 0; i < n; i++) {
                    if (token.IsCancellationRequested) break;
                    for (int j = i + 1; j < n; j++) {
                        double sim = ImageHasher.CalculateSimilarity(items[i], items[j]);
                        if (sim >= similarityThreshold) {
                            dsu.Union(i, j);
                            string key = i < j ? i + "_" + j : j + "_" + i;
                            pairSimilarities[key] = sim;
                        }
                    }
                }

                // Group by cluster
                var clusters = new Dictionary<int, List<int>>();
                for (int i = 0; i < n; i++) {
                    int root = dsu.Find(i);
                    if (!clusters.ContainsKey(root)) {
                        clusters[root] = new List<int>();
                    }
                    clusters[root].Add(i);
                }

                var resultGroups = new List<SimilarGroup>();
                int groupCounter = 1;

                foreach (var kvp in clusters) {
                    if (kvp.Value.Count < 2) continue; // Not a duplicate/similar group

                    var groupItems = kvp.Value.Select(idx => items[idx]).ToList();

                    // Sort items in group: highest resolution first, then largest file size, then newest date
                    groupItems.Sort((a, b) => {
                        long resA = (long)a.Width * a.Height;
                        long resB = (long)b.Width * b.Height;
                        if (resB != resA) return resB.CompareTo(resA);
                        if (b.FileSizeBytes != a.FileSizeBytes) return b.FileSizeBytes.CompareTo(a.FileSizeBytes);
                        return b.DateTaken.CompareTo(a.DateTaken);
                    });

                    // Determine max similarity in group
                    double maxSim = 0;
                    for (int i = 0; i < kvp.Value.Count; i++) {
                        for (int j = i + 1; j < kvp.Value.Count; j++) {
                            int idx1 = kvp.Value[i];
                            int idx2 = kvp.Value[j];
                            string key = idx1 < idx2 ? idx1 + "_" + idx2 : idx2 + "_" + idx1;
                            double s;
                            if (pairSimilarities.TryGetValue(key, out s)) {
                                if (s > maxSim) maxSim = s;
                            }
                        }
                    }
                    if (maxSim < 0.1) maxSim = similarityThreshold;

                    string badge = maxSim >= 99.5 ? "Exact Duplicate" : string.Format("{0:F0}% Match", maxSim);

                    var group = new SimilarGroup {
                        GroupId = "G" + groupCounter++,
                        DateHeader = groupItems[0].DateFormatted,
                        MaxSimilarity = maxSim,
                        SimilarityBadge = badge
                    };

                    foreach (var itm in groupItems) {
                        group.Photos.Add(itm);
                    }

                    resultGroups.Add(group);
                }

                // Sort groups by date descending
                resultGroups.Sort((g1, g2) => {
                    DateTime dt1 = g1.Photos.Count > 0 ? g1.Photos[0].DateTaken : DateTime.MinValue;
                    DateTime dt2 = g2.Photos.Count > 0 ? g2.Photos[0].DateTaken : DateTime.MinValue;
                    return dt2.CompareTo(dt1);
                });

                return resultGroups;
            });
        }

        public static async Task<List<ReferenceMatchItem>> ScanByReferenceAsync(
            List<string> referencePaths,
            string targetFolder,
            bool recursive,
            double similarityThreshold,
            Action<ScanProgressInfo> progressCallback,
            CancellationToken token) {

            return await Task.Run(() => {
                var matches = new List<ReferenceMatchItem>();
                if (referencePaths == null || referencePaths.Count == 0 || !Directory.Exists(targetFolder)) {
                    return matches;
                }

                // Extract reference items
                var refItems = new List<ImageItem>();
                foreach (var refPath in referencePaths) {
                    try {
                        var rInfo = ImageHasher.ExtractImageInfo(refPath);
                        if (rInfo != null) refItems.Add(rInfo);
                    } catch { }
                }

                if (refItems.Count == 0) return matches;

                var files = GetImageFiles(targetFolder, recursive, token);
                // Exclude reference files themselves
                var refSet = new HashSet<string>(referencePaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
                files = files.Where(f => !refSet.Contains(Path.GetFullPath(f))).ToList();

                int total = files.Count;
                int processed = 0;
                object syncLock = new object();

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (file, state) => {
                    if (token.IsCancellationRequested) {
                        state.Stop();
                        return;
                    }

                    try {
                        var targetInfo = ImageHasher.ExtractImageInfo(file);
                        if (targetInfo != null) {
                            double bestSim = 0;
                            ImageItem matchedRef = null;

                            foreach (var rItem in refItems) {
                                double sim = ImageHasher.CalculateSimilarity(rItem, targetInfo);
                                if (sim > bestSim) {
                                    bestSim = sim;
                                    matchedRef = rItem;
                                }
                            }

                            if (bestSim >= similarityThreshold && matchedRef != null) {
                                string badge = bestSim >= 99.5 ? "Exact Duplicate" : string.Format("{0:F0}% Match", bestSim);
                                lock (syncLock) {
                                    matches.Add(new ReferenceMatchItem {
                                        ReferencePhoto = matchedRef,
                                        MatchedPhoto = targetInfo,
                                        Similarity = bestSim,
                                        SimilarityBadge = badge,
                                        IsSelected = false
                                    });
                                }
                            }
                        }
                    } catch { }

                    int current = Interlocked.Increment(ref processed);
                    if (progressCallback != null && (current % 5 == 0 || current == total)) {
                        progressCallback(new ScanProgressInfo {
                            Current = current,
                            Total = total,
                            CurrentFileName = Path.GetFileName(file),
                            StatusMessage = string.Format("Scanning album: {0} of {1} photos...", current, total)
                        });
                    }
                });

                // Sort matches by similarity descending
                matches.Sort((m1, m2) => m2.Similarity.CompareTo(m1.Similarity));
                return matches;
            });
        }
    }

    public class DisjointSet {
        private int[] _parent;

        public DisjointSet(int size) {
            _parent = new int[size];
            for (int i = 0; i < size; i++) {
                _parent[i] = i;
            }
        }

        public int Find(int i) {
            if (_parent[i] == i) return i;
            return _parent[i] = Find(_parent[i]);
        }

        public void Union(int i, int j) {
            int rootI = Find(i);
            int rootJ = Find(j);
            if (rootI != rootJ) {
                _parent[rootI] = rootJ;
            }
        }
    }
}
