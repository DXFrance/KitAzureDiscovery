using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using WebDecouverteAzure.Services;

namespace WebDecouverteAzure.Controllers
{
    public class ImagesController : Controller
    {
        private readonly ParametrageService _parametrageService;

        public ImagesController()
        {
            _parametrageService = new ParametrageService();
        }

        /// <summary>
        /// Retourne la liste des images de la date situés dans le dossier /Datas triés par date
        /// </summary>
        [HttpGet]
        public JsonResult GetImages(DateTime date)
        {
            var folder = ControllerContext.HttpContext.Server.MapPath("/Datas");
            var files = Directory.GetFiles(folder,
                string.Format("{0}-{1}-{2}*.jpg",
                    date.Year.ToString("0000"),
                    date.Month.ToString("00"),
                    date.Day.ToString("00")));

            return Json(files
                .Select(file => new {Name = Path.GetFileName(file)})
                .OrderByDescending(f => f.Name),
                JsonRequestBehavior.AllowGet);
        }
        /// <summary>
        /// Méthode appelées par les clients pour enregistrer une image capturée par la webcam
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public JsonResult UploadImage()
        {
            byte[] datas;
            using (var reader = new BinaryReader(Request.InputStream))
                datas = reader.ReadBytes((int) Request.InputStream.Length);

            SaveBitmap(datas);
            return Json(new
            {
                Duration = _parametrageService.LoadDuration()
            });
        }
        [HttpPost]
        public JsonResult DeleteImage(string fileName)
        {
            var folder = ControllerContext.HttpContext.Server.MapPath("/Datas");
            var filename = Path.Combine(folder, fileName);
            if (System.IO.File.Exists(filename))
                System.IO.File.Delete(filename);

            return Json("");
        }
        /// <summary>
        /// Enregistre le bitmap dans le dossier /Datas
        /// </summary>
        private void SaveBitmap(byte[] datas)
        {
            var date = DateTime.UtcNow;

            var folder = ControllerContext.HttpContext.Server.MapPath("/Datas");
            var filename = Path.Combine(
                folder,
                string.Format("{0}-{1}-{2} {3}-{4}-{5}.jpg",
                    date.Year.ToString("0000"),
                    date.Month.ToString("00"),
                    date.Day.ToString("00"),
                    date.Hour.ToString("00"),
                    date.Minute.ToString("00"),
                    date.Second.ToString("00")));

            using (var mem = new MemoryStream(datas))
            {
                var bitmap = new Bitmap(mem);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.DrawString(
                        string.Format("{0} {1} (UTC)", DateTime.UtcNow.ToLongDateString(), DateTime.UtcNow.ToLongTimeString()),
                        new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel),
                        Brushes.White, 5, 5);
                }
                var myEncoderParameters = new EncoderParameters(1);
                myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
                bitmap.Save(filename, GetJpegEncoder(), myEncoderParameters);
            }
        }
        private static ImageCodecInfo GetJpegEncoder()
        {
            return ImageCodecInfo
                .GetImageDecoders()
                .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        }
    }
}