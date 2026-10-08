using System;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace SimilarPhotoFinder {
    public static class FileOperations {
        public static bool DeleteFile(string filePath, bool sendToRecycleBin, out string errorMessage) {
            errorMessage = null;
            try {
                if (!File.Exists(filePath)) return true;

                if (sendToRecycleBin) {
                    FileSystem.DeleteFile(
                        filePath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.DoNothing
                    );
                } else {
                    File.Delete(filePath);
                }
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
