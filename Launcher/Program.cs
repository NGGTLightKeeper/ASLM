using System.Diagnostics;

const string folderName = "App";
const string exeName = "ASLM.exe";

var currentDir = AppDomain.CurrentDomain.BaseDirectory;
var targetPath = Path.Combine(currentDir, folderName, exeName);

if (File.Exists(targetPath))
{
    var startInfo = new ProcessStartInfo
    {
        FileName = targetPath,
        WorkingDirectory = Path.Combine(currentDir, folderName),
        UseShellExecute = false
    };

    try
    {
        Process.Start(startInfo);
    }
    catch (Exception) {}
}
