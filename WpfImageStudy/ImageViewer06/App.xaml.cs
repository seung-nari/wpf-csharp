using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace ImageViewer06
{
    public partial class App : Application
    {
        [DllImport("kernel32", SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            // 1순위: gdal\x64 (지금 패키지 구조상 이쪽일 가능성 큼)
            var nativeDir = Path.Combine(exeDir, "gdal", "x64");

            // 2순위: runtimes\win-x64\native (존재하면 이것도 지원)
            var nativeDir2 = Path.Combine(exeDir, "runtimes", "win-x64", "native");

            if (Directory.Exists(nativeDir))
                SetDllDirectory(nativeDir);
            else if (Directory.Exists(nativeDir2))
                SetDllDirectory(nativeDir2);
            else
            {
                MessageBox.Show("native dir not found:\n" + nativeDir + "\n" + nativeDir2);
                Shutdown();
                return;
            }

            // GDAL_DATA / PROJ_LIB (지금 gdal\data / gdal\share 구조에 맞춤)
            var gdalData = Path.Combine(exeDir, "gdal", "data");
            if (Directory.Exists(gdalData))
                Environment.SetEnvironmentVariable("GDAL_DATA", gdalData);

            // PROJ는 보통 share\proj
            var projLib = Path.Combine(exeDir, "gdal", "share", "proj");
            if (Directory.Exists(projLib))
                Environment.SetEnvironmentVariable("PROJ_LIB", projLib);

            OSGeo.GDAL.Gdal.AllRegister();

        }
    }
}
