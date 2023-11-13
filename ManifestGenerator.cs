using Newtonsoft.Json.Linq;
using NSec.Cryptography;
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
	public const string BUILDS_PATH = "/home/ss14/ssmvEngine/release/";
	public const string BUILDS_URL = "https://cdn.spacestationmultiverse.com/ssmv-engine-builds/";
	private const string PRIVATE_KEY_PATH = "ssmvEngine.key";

	public static void Main(string[] args)
	{
		if (args.Length != 1)
		{
			Console.WriteLine("Please pass the SHA");
			return;
		}
		string version = args[0];

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
			var buildJson = GenerateJsonForBuild(version);
			manifestJson[version] = buildJson;
		} catch (Exception e) {
			Console.WriteLine("Error generating json for SHA: " + e.Message);
			return;
		}

		// Output
		File.WriteAllText(MANIFEST_FILEPATH, manifestJson.ToString(Newtonsoft.Json.Formatting.Indented));
		//Console.WriteLine(manifestJson.ToString(Newtonsoft.Json.Formatting.Indented));
	}

	private static JObject GenerateJsonForBuild(string version)
	{
		var json = new JObject();
		json["time"] = DateTime.Now.ToString("o"); // Not necessary for an engine manifest, but seems like it might be useful?
		json["insecure"] = false;

		// Create definition for each platform .zip available
		var jsonPlatforms = new JObject();
		foreach (var path in Directory.EnumerateFiles(PathForBuild(version)))
		{
			var regex = @"(.*)Robust.Client_(.+)\.zip";
			var regexResult = Regex.Match(path, regex);
			if (regexResult.Success)
			{
				string platform = regexResult.Groups[2].Value;

				var platformJson = new JObject();
				var fileInfo = new FileInfo(path);
				platformJson["url"] = BUILDS_URL + version + '/' + fileInfo.Name;
				platformJson["sha256"] = SHA256CheckSum(path);
				platformJson["sig"] = GenerateSignature(path);
				jsonPlatforms[platform] = platformJson; 
			}
		}
		json["platforms"] = jsonPlatforms;

		return json;
	}

	private static string PathForBuild(string version)
	{
		return BUILDS_PATH + version + Path.DirectorySeparatorChar;
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

	private static string GenerateSignature(string filePath)
	{
		var privateKeyFile = File.ReadAllBytes(PRIVATE_KEY_PATH);
		var key = Key.Import(SignatureAlgorithm.Ed25519, privateKeyFile, KeyBlobFormat.PkixPrivateKeyText);

		var fileContents = File.ReadAllBytes(filePath);
        var signature = SignatureAlgorithm.Ed25519.Sign(key, fileContents);
        return BitConverter.ToString(signature).Replace("-", "");
	}

	/// <summary>
	/// First-time setup of key signing pair for engine build signing
	/// </summary>
	/*
	private static void GenerateSigningKeyPair()
	{
		// select the Ed25519 signature algorithm
		var algorithm = SignatureAlgorithm.Ed25519;

		// create a new key pair
		var policyAllowExport = new KeyCreationParameters();
		policyAllowExport.ExportPolicy = KeyExportPolicies.AllowPlaintextExport;
		using var key = Key.Create(algorithm, policyAllowExport);

		// generate some data to be signed
		File.WriteAllBytes("ssmvEngine.key", key.Export(KeyBlobFormat.PkixPrivateKeyText));
		File.WriteAllBytes("ssmvEngine.public", key.Export(KeyBlobFormat.PkixPublicKeyText));
	}
	*/
}
