using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using BsDiff;
using Newtonsoft.Json.Linq;
using QuestPatcher.Zip;

namespace EchoVRCEQuestPatcher
{
    internal class Program
    {
        public const string APK_HASH = "21c7dc914dba2fa44f8daf019aedffa4a17e14186283ef05805217fc80d30eaf";
        public const string OBB_HASH = "8020d1791c8806b3c9592806d2476c9b9c9771cbfcbce112914b357b66180607";
        public const string ADB_DOWNLOAD = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

        public static readonly string ADB_ROOT = Path.Join(Path.GetTempPath(), "platform-tools");
        public static readonly string ADB_DIR = Path.Join(ADB_ROOT, "platform-tools");
        public static readonly string ADB_PATH = Path.Join(ADB_DIR, "adb.exe");

        class SimpleShellOutputReciever : IShellOutputReceiver
        {
            public bool ParsesErrors
            {
                get => false;
            }
            public string Output = "";

            public void AddOutput(string data)
            {
                Output += data + "\n";
            }

            public void Flush()
            {
            }
        }

        public static readonly string[] deviceNames =
        {
            "vr_monterey",        // Oculus Quest 1
            "hollywood",         // Oculus Quest 2
            "seacliff",         // Oculus Quest Pro
            "eureka",          // Oculus Quest 3
        };

        static string GetSha256(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static void Main()
        {
            Console.WriteLine("Echo VR: CE Quest Patcher");
            Console.WriteLine("By: @unusualnorm");

            #region Download ADB Server
            Console.WriteLine("Downloading ADB Server...");
            if (!Directory.Exists(ADB_ROOT))
                Directory.CreateDirectory(ADB_ROOT);
            if (!File.Exists(ADB_PATH))
                try
                {
                    var adbZipPath = Path.Join(ADB_ROOT, "platform-tools.zip");
                    var httpClient = new HttpClient();
                    var adbZipFile = httpClient.GetStreamAsync(ADB_DOWNLOAD).Result;
                    var adbZipFileStream = File.OpenWrite(adbZipPath);
                    adbZipFile.CopyTo(adbZipFileStream);
                    adbZipFileStream.Dispose();
                    adbZipFile.Dispose();
                    httpClient.Dispose();

                    ZipFile.ExtractToDirectory(adbZipPath, ADB_ROOT);
                    File.Delete(adbZipPath);
                } catch (Exception e)
                {
                    Console.WriteLine("Failed to download ADB Server");
                    Console.WriteLine(e);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }
            Console.WriteLine("Downloaded ADB Server!");
            #endregion

            #region Start ADB Server
            Console.WriteLine("Starting ADB Server...");
            try
            {
                AdbServer.Instance.StartServer(ADB_PATH, false);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start ADB Server");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Started ADB Server!");
            #endregion

            #region Connect to ADB Server
            Console.WriteLine("Connecting to ADB Server...");
            var client = new AdbClient();
            try
            {
                client.Connect("127.0.0.1");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to connect to ADB Server");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Connected to adb server!");
            #endregion

            #region Find device
            Console.WriteLine("Finding device...");
            DeviceData? device = null;
            try
            {
                while (device == null)
                {
                    var devices = client.GetDevices();
                    foreach (var d in devices)
                    {
                        if (!deviceNames.Contains(d.Product)) continue;
                        device = d;
                    }

                    if (device != null) break;
                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to find device");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine($"Found device: {device.Product}");
            #endregion

            #region Get Echo VR APK path
            Console.WriteLine("Getting Echo VR APK path...");
            string apkPath = "r15_goldmaster_store.apk";
            if (!File.Exists(apkPath))
            {
                Console.WriteLine("Failed to find Echo VR APK");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine($"Echo VR APK path: {apkPath}");
            #endregion

            #region Check Echo VR APK hash
            Console.WriteLine("Checking Echo VR APK hash...");
            string apkHash;
            try
            {
                using var apkStream = File.OpenRead(apkPath);
                apkHash = GetSha256(apkStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to get Echo VR APK hash");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            if (apkHash != APK_HASH)
            {
                Console.WriteLine($"Echo VR APK hash is not correct");
                Console.WriteLine($"Please make sure you have downloaded the non-farewell version of Echo VR");
                Console.WriteLine($"Expected: {APK_HASH}");
                Console.WriteLine($"Actual: {apkHash}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Echo VR APK hash is correct!");
            #endregion

            #region Get Echo VR OBB path
            Console.WriteLine("Getting Echo VR OBB path...");
            var obbPath = "main.4987566.com.readyatdawn.r15.obb";
            if (!File.Exists(obbPath))
            {
                Console.WriteLine("Failed to find Echo VR OBB");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            #endregion

            #region Start Echo VR OBB stream
            Console.WriteLine("Starting Echo VR OBB stream...");
            Stream obbStream;
            try
            {
                obbStream = File.OpenRead(obbPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start Echo VR OBB stream");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Started Echo VR OBB stream!");
            #endregion

            #region Check Echo VR OBB hash
            Console.WriteLine("Checking Echo VR OBB hash...");
            string obbHash;
            try
            {
                obbHash = GetSha256(obbStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to get Echo VR OBB hash");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            if (obbHash != OBB_HASH)
            {
                Console.WriteLine($"Echo VR OBB hash is not correct");
                Console.WriteLine($"Please make sure you have downloaded the non-farewell version of Echo VR");
                Console.WriteLine($"Expected: {OBB_HASH}");
                Console.WriteLine($"Actual: {obbHash}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Echo VR OBB hash is correct!");
            #endregion

            #region Extract Echo VR APK
            Console.WriteLine("Extracting Echo VR APK...");
            var extractedApkDir = Path.Join(Path.GetTempPath(), "echovrcequestpatcherextractedapk");
            if (Directory.Exists(extractedApkDir))
                Directory.Delete(extractedApkDir, true);
            Directory.CreateDirectory(extractedApkDir);
            try
            {
                var apkZip = ZipFile.OpenRead(apkPath);
                apkZip.ExtractToDirectory(extractedApkDir);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to extract Echo VR APK");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Extracted Echo VR APK!");
            #endregion

            #region Patch libpnsovr.so
            Console.WriteLine("Patching libpnsovr.so...");
            var libPnsOvrPath = Path.Join(extractedApkDir, "lib", "arm64-v8a", "libpnsovr.so");
            var newLibPnsOvrPath = Path.Join(extractedApkDir, "lib", "arm64-v8a", "libpnsovr.so.new");
            try
            {
                var libPnsOvrStream = File.OpenRead(libPnsOvrPath);
                var newLibPnsOvrStream = File.OpenWrite(newLibPnsOvrPath);
                BinaryPatch.Apply(libPnsOvrStream, () => Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoVRCEQuestPatcher.libpnsovr_patch"), newLibPnsOvrStream);
                libPnsOvrStream.Dispose();
                newLibPnsOvrStream.Dispose();
                File.Delete(libPnsOvrPath);
                File.Move(newLibPnsOvrPath, libPnsOvrPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to patch libpnsovr.so");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Patched libpnsovr.so!");
            #endregion

            #region Insert config.json
            Console.WriteLine("Inserting config.json...");
            var configJsonDir = Path.Join(extractedApkDir, "assets", "_local");
            var configJsonPath = Path.Join(configJsonDir, "config.json");
            try
            {
                Directory.CreateDirectory(configJsonDir);
                using var configJsonStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoVRCEQuestPatcher.config.json");
                using var configJsonFile = File.OpenWrite(configJsonPath);
                configJsonStream!.CopyTo(configJsonFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to insert config.json");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Inserted config.json!");
            #endregion

            #region Change config.json
            Console.WriteLine("Changing config.json...");
            try
            {
                var configJson = JObject.Parse(File.ReadAllText(configJsonPath));
                configJson["loginservice_host"] = configJson["loginservice_host"] + device.Serial;
                File.WriteAllText(configJsonPath, configJson.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to change config.json");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Changed config.json!");
            #endregion

            #region Remove META-INF
            Console.WriteLine("Removing META-INF...");
            var metaInfDir = Path.Join(extractedApkDir, "META-INF");
            try
            {
                Directory.Delete(metaInfDir, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to remove META-INF");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Removed META-INF!");
            #endregion

            #region Create new Echo VR APK
            Console.WriteLine("Creating new Echo VR APK...");
            try
            {
                if (File.Exists("r15_goldmaster_store_patched.apk"))
                    File.Delete("r15_goldmaster_store_patched.apk");
                ZipFile.CreateFromDirectory(extractedApkDir, "r15_goldmaster_store_patched.apk");
                Directory.Delete(extractedApkDir, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to extract Echo VR APK");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Created new Echo VR APK!");
            #endregion

            #region Sign new Echo VR APK
            Console.WriteLine("Signing new Echo VR APK...");
            try
            {
                var newApkStream = File.Open("r15_goldmaster_store_patched.apk", FileMode.Open);
                ApkZip.Open(newApkStream).Dispose();

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to sign new Echo VR APK");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Signed new Echo VR APK!");
            #endregion

            #region Uninstall Echo VR
            Console.WriteLine("Uninstalling Echo VR...");
            try
            {
                var appVersion = client.GetPackageVersion(device, "com.readyatdawn.r15");
                if (appVersion != null)
                    client.UninstallPackage(device, "com.readyatdawn.r15");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to uninstall Echo VR");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Uninstalled Echo VR!");
            #endregion

            #region Install Echo VR
            Console.WriteLine("Installing Echo VR...");
            try
            {
                var newApkStream = File.OpenRead("r15_goldmaster_store_patched.apk");
                newApkStream.Position = 0;
                client.Install(device, newApkStream);
                newApkStream.Dispose();
                File.Delete("r15_goldmaster_store_patched.apk");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to install Echo VR");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Installed Echo VR!");
            #endregion

            #region Install Echo VR OBB
            Console.WriteLine("Installing Echo VR OBB...");
            try
            {
                obbStream.Position = 0;
                client.ExecuteRemoteCommand($"mkdir -p /sdcard/Android/obb/com.readyatdawn.r15", device, null);
                client.Push(device, "/sdcard/Android/obb/com.readyatdawn.r15/main.4987566.com.readyatdawn.r15.obb", obbStream, 0, new DateTimeOffset());
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to install Echo VR OBB");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Installed Echo VR OBB!");
            #endregion

            Console.WriteLine("Successfully installed Echo VR: CE!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
    }
}
