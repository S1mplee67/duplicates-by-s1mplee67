using System;
using System.Windows;

namespace SimilarPhotoFinder {
    public class Program : Application {
        [STAThread]
        public static void Main() {
            var app = new Program();
            var win = new MainWindow();
            app.Run(win);
        }
    }
}
