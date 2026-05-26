using OpenSilver;
using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Database_Designer
{
    public static class ImageHelper
    {
        public static void SelectAndPreviewImage(System.Windows.Controls.Image targetImage, Action<byte[]> onBytesReady = null)
        {
            targetImage.Source = null;
            SelectAndLoadBytes(new Action<object>(obj =>
            {
                var base64 = obj?.ToString();
                if (string.IsNullOrEmpty(base64))
                {
                    Console.WriteLine("FileReader failed or was cancelled.");
                    return;
                }
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    var bitmap = BytesToBitmapImage(bytes);
                    targetImage.Source = bitmap;
                    onBytesReady?.Invoke(bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to load image: " + ex.Message);
                }
            }));
        }

        public static void SelectAndLoadBytes(Action<object> onBytesReady)
        {
            var fileInput = Interop.ExecuteJavaScript("document.createElement('input')");
            Interop.ExecuteJavaScript("$0.type = 'file'; $0.accept = 'image/*'; $0.style.display = 'none';", fileInput);

            Interop.ExecuteJavaScript(@"
(function(input, callback) {
    input.onchange = function() {
        var file = input.files[0];
        if (!file) return;
        var reader = new FileReader();
        reader.onload = function(evt) {
            var dataUrl = evt.target.result;
            if (!dataUrl || typeof dataUrl !== 'string') { callback(null); return; }
            var comma = dataUrl.indexOf(',');
            if (comma < 0) { callback(null); return; }
            var base64 = dataUrl.substring(comma + 1);
            callback(base64);
            try { if (input.parentNode) input.parentNode.removeChild(input); } catch(e) {}
        };
        reader.onerror = function() {
            callback(null);
            try { if (input.parentNode) input.parentNode.removeChild(input); } catch(e) {}
        };
        reader.readAsDataURL(file);
    };
    input.value = '';
    document.body.appendChild(input);
    setTimeout(function(){ input.click(); }, 50);
})($0, $1);
", fileInput, onBytesReady);
        }

        public static void SelectAndLoadBytes(Action<byte[]> onBytesReady)
        {
            SelectAndLoadBytes(new Action<object>(obj =>
            {
                var base64 = obj?.ToString();
                if (string.IsNullOrEmpty(base64))
                {
                    Console.WriteLine("FileReader failed or was cancelled.");
                    return;
                }
                try
                {
                    onBytesReady?.Invoke(Convert.FromBase64String(base64));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to load image: " + ex.Message);
                }
            }));
        }

        public static BitmapImage BytesToBitmapImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Byte array is null or empty.");

            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                ms.Position = 0;
                bitmap.SetSource(ms);
            }
            return bitmap;
        }
    }
}