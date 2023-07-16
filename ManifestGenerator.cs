using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

/// <summary>
/// The manifest generator updates the Manifest.json file to include the 
/// target build file.
/// </summary>
public static class ManifestGenerator
{
	public const string MANIFEST_FILEPATH = "manifest.json";
	public const bool ALLOW_NEW_FILE = true; // enable for first run
	public const string BUILDS_PATH = "/home/ss14/space-station-14/release/";
	public const string BUILDS_URL = "https://cdn.blepstation.com/blep-builds/";

	public static void Main(string[] args)
	{
		if (args.Length != 1)
		{
			Console.WriteLine("Please pass the SHA");
			return;
		}
		string targetSHA = args[0];

		// Load old manifest
		JObject manifestJson;
		try
		{
			string rawJson = File.ReadAllText(MANIFEST_FILEPATH);
			manifestJson = JObject.Parse(rawJson);

		} catch (FileNotFoundException e) {
			if (ALLOW_NEW_FILE)
			{
				Console.WriteLine("No existing json, creating...");
				manifestJson = new JObject();
			} else {
				Console.WriteLine("No existing json found -- exitting.  (First run should be done with ALLOW_NEW_FILE set to true)");
				return;
			}
		} catch (Exception e) {
			Console.WriteLine("Error loading old json: " + e.Message);
			return;
		}
		
		// Generate json for target build
		try
		{
			var buildJson = GenerateJsonForBuild(targetSHA);
			manifestJson[targetSHA] = buildJson;
		} catch (Exception e) {
			Console.WriteLine("Error generating json for SHA: " + e.Message);
			return;
		}

		// Output
		File.WriteAllText(MANIFEST_FILEPATH, manifestJson.ToString(Newtonsoft.Json.Formatting.Indented));
		//Console.WriteLine(manifestJson.ToString(Newtonsoft.Json.Formatting.Indented));
	}

	private static JObject GenerateJsonForBuild(string targetSHA)
	{
		var json = new JObject();
		json["time"] = DateTime.Now.ToString("o");

		// Client build
		var jsonClient = new JObject();
		jsonClient["url"] = BUILDS_URL + targetSHA + '/' + "SS14.Client.zip";
		jsonClient["sha256"] = SHA256CheckSum(PathForBuild(targetSHA) + "SS14.Client.zip");
		json["client"] = jsonClient;

		// Server builds
		var jsonServer = new JObject();
		foreach (var path in Directory.EnumerateFiles(PathForBuild(targetSHA)))
		{
			var regex = @"(.*)SS14.Server_(.+)\.zip";
			var regexResult = Regex.Match(path, regex);
			if (regexResult.Success)
			{
				string platform = regexResult.Groups[2].Value;

				var platformJson = new JObject();
				var fileInfo = new FileInfo(path);
				platformJson["url"] = BUILDS_URL + targetSHA + '/' + fileInfo.Name;
				platformJson["sha256"] = SHA256CheckSum(path);
				jsonServer[platform] = platformJson; 
			}
		}
		json["server"] = jsonServer;

		return json;
	}

	private static string PathForBuild(string targetSHA)
	{
		return BUILDS_PATH + targetSHA + Path.DirectorySeparatorChar;
	}

	private static string SHA256CheckSum(string filePath)
	{
		using (SHA256 SHA256 = SHA256Managed.Create())
		{
			using (FileStream fileStream = File.OpenRead(filePath))
				return Convert.ToHexString(SHA256.ComputeHash(fileStream));
				//return Convert.ToBase64String(SHA256.ComputeHash(fileStream));
		}
	}
}
