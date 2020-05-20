using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MessagePack;

namespace Coflnet {
	public static class FileController {

		static string _dataPaht;

		public static string dataPaht
		{get{
			return _dataPaht;
		}set{
			Directory.CreateDirectory (value);
			_dataPaht = value;
		}}

		public static string configPath = "/etc/coflnet";

		static FileController () {
			//dataPaht = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + dataPostFix;

			
		}

		public static IEnumerable<string> FileNames (string namePattern = "*", string subFolder = "") {
			return Directory.GetFiles (Path.Combine (dataPaht, subFolder), namePattern).Select (Path.GetFileName);
		}

		public static IEnumerable<string> DirectoriesNames (string namePattern = "*", string subFolder = "") {
			return Directory.GetDirectories (Path.Combine (dataPaht, subFolder), namePattern);
		}

		/// <summary>
		/// Writes all text.
		/// </summary>
		/// <param name="path">Path.</param>
		/// <param name="text">Text.</param>
		public static void WriteAllText (string path, string text) {
			File.WriteAllText (Path.Combine (dataPaht, path), text);
		}

		/// <summary>
		/// Writes all bytes.
		/// Creates folder if it doesn't exist
		/// </summary>
		/// <param name="path">Path.</param>
		/// <param name="bytes">Bytes.</param>
		public static void WriteAllBytes (string path, byte[] bytes) {
			System.IO.FileInfo file = new FileInfo (Path.Combine (dataPaht, path));
			file.Directory.Create ();
			File.WriteAllBytes (file.FullName, bytes);
		}

		/// <summary>
		/// Reads all bytes.
		/// </summary>
		/// <returns>The all bytes.</returns>
		/// <param name="relativePath">Path.</param>
		public static byte[] ReadAllBytes (string relativePath) {
			return File.ReadAllBytes (Path.Combine (dataPaht, relativePath));
		}

		/// <summary>
		/// Reads all config bytes.
		/// </summary>
		/// <returns>The all config bytes.</returns>
		/// <param name="relativePath">Relative path.</param>
		public static byte[] ReadAllConfigBytes (string relativePath) {
			return File.ReadAllBytes (Path.Combine (configPath, relativePath));
		}

		/// <summary>
		/// Determines whether the specified File exists relative to the settings direcotry
		/// </summary>
		/// <returns>The exists.</returns>
		/// <param name="path">Path.</param>
		public static bool SettingExits (string path) {
			return File.Exists (Path.Combine (configPath, path));
		}

		/// <summary>
		/// Determines whether the specified File exists relative to the data direcotry
		/// </summary>
		/// <returns>The exists.</returns>
		/// <param name="path">Path.</param>
		public static bool Exists (string path) {
			return File.Exists (Path.Combine (dataPaht, path));
		}

		/// <summary>
		/// Reads Line by line and returns it as
		/// </summary>
		/// <returns>The lines as.</returns>
		/// <param name="relativePath">Relative path.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static IEnumerable<T> ReadLinesAs<T> (string relativePath, Action corrupted = null) {
			return ReadLinesAs<T> (relativePath, MessagePackSerializer.DefaultResolver,corrupted);
		}

		/// <summary>
		/// Reads the lines and deserialize as specific object.
		/// </summary>
		/// <returns>The lines as.</returns>
		/// <param name="relativePath">Relative path.</param>
		/// <param name="resolver">Resolver.</param>
		/// <typeparam name="T">The type to deserialize to.</typeparam>
		public static IEnumerable<T> ReadLinesAs<T> (string relativePath, IFormatterResolver resolver,Action corrupted=null, int brokenThreshold = 1000) {
			var path = Path.Combine (dataPaht, relativePath);
			if (!File.Exists (path)) {
				// there is nothing to read
				yield break;
			}

			int brokenCount = 0;

			using (var file = File.Open (path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				while (file.Position < file.Length) {
					T element;
					try{
						element = MessagePackSerializer.Deserialize<T> (file, resolver, true);
					} catch(Exception )
					{
						brokenCount++;
						continue;
					}
					if(brokenCount> brokenThreshold)
					{
						break;
					}

					yield return element;
				}
			}
			if(brokenCount > 0)
				corrupted?.Invoke();
		}

		/// <summary>
		/// Appends an object to file after serializing it
		/// </summary>
		/// <param name="relativePath">Relative path.</param>
		/// <param name="data">The object to serialize.</param>
		/// <typeparam name="T">Type to serialize to.</typeparam>
		public static void AppendLineAs<T> (string relativePath, T data) {
			AppendLineAs<T> (relativePath, data, MessagePackSerializer.DefaultResolver);
		}

		/// <summary>
		/// Appends an object to a file after serializing it
		/// </summary>
		/// <param name="relativePath">Relative path.</param>
		/// <param name="data">Data.</param>
		/// <param name="resolver">Resolver.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static void AppendLineAs<T> (string relativePath, T data, IFormatterResolver resolver) {
			Directory.CreateDirectory (Path.GetDirectoryName (Path.Combine (dataPaht, relativePath)));
			using (var file = File.Open (Path.Combine (dataPaht, relativePath), FileMode.Append, FileAccess.Write, FileShare.None)) {
				MessagePackSerializer.Serialize<T> (file, data, resolver);
			}
		}

		/// <summary>
		/// Reads Line by line and returns it as
		/// </summary>
		/// <returns>The lines as.</returns>
		/// <param name="relativePath">Relative path.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static void WriteLinesAs<T> (string relativePath, IEnumerable<T> data) {
			WriteLinesAs<T> (relativePath, data, MessagePackSerializer.DefaultResolver);
		}

		/// <summary>
		/// Serializes and writes objects to a file relative to the application data folder
		/// </summary>
		/// <param name="relativePath">Relative path to the data folder.</param>
		/// <param name="data">Data to write.</param>
		/// <param name="resolver">Resolver to use for serialization.</param>
		/// <typeparam name="T">What type to use for serialization.</typeparam>
		public static void WriteLinesAs<T> (string relativePath, IEnumerable<T> data, IFormatterResolver resolver) {
			using (var file = File.Open (Path.Combine (dataPaht, relativePath), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)) {
				foreach (var item in data) {
					MessagePackSerializer.Serialize<T> (file, item, resolver);
				}
			}
		}


		public static void ReplaceLine<T>(string relativePath, Func<T, bool> target, T data,bool appendIfNotFound = true)
		{
			ReplaceLine<T>(relativePath,target,data,appendIfNotFound,MessagePackSerializer.DefaultResolver);
		}

		/// <summary>
		/// Reads in a file, searches it for objects and if found replaces matched objects with given data
		/// </summary>
		/// <param name="relativePath">Relative path to the file</param>
		/// <param name="target">Evaluates if a given object should be replaced</param>
		/// <param name="data">The data to be replaced with</param>
		/// <param name="data">The data to be replaced with</param>
		/// <param name="appendIfNotFound">Whereether or not the data should be appended to the end if no match was found</param>
		/// <typeparam name="T"></typeparam>
		public static void ReplaceLine<T> (string relativePath, Func<T, bool> target, T data,bool appendIfNotFound, IFormatterResolver resolver, short retryNum = 0) {
			var path = Path.Combine (dataPaht, relativePath);
			// create it
			Directory.CreateDirectory (Path.GetDirectoryName (path));
			var tempPath = path + ".tmp";
			lock(tempPath)
			{
				bool found = false;
				using (var file = File.Open (path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read)) {
					using (var tempFile = File.Open (tempPath, FileMode.Append, FileAccess.Write, FileShare.Read)) {
						while (file.Position < file.Length) {
							try{
								var item = MessagePackSerializer.Deserialize<T> (file, resolver, true);
								
								if (target.Invoke (item)) {
									item = data;
									found = true;
								}
								MessagePackSerializer.Serialize<T> (tempFile, item, resolver);
							} catch(InvalidOperationException)
							{
								Console.WriteLine($"Skipped correupted element {relativePath}");
							}
							
							
						}

						// may append it to the end if it didn't exist bevore
						if(appendIfNotFound && !found)
						{
							MessagePackSerializer.Serialize<T> (tempFile, data, resolver);
						}
					}
				}
				File.Delete(path);
				try{
					File.Move(tempPath,path);
				} catch(IOException)
				{
					Console.WriteLine($"Could not move {tempPath} retrying");
					if(retryNum < 3)
						ReplaceLine<T>(relativePath,target,data,appendIfNotFound,resolver,retryNum++);
						else {
							Console.WriteLine($"To many tries");
						}

				}
			}
			
		}

		/// <summary>
		/// Loads data and tries to serialize it into given type
		/// </summary>
		/// <returns>The as.</returns>
		/// <param name="relativePath">Relative path.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static T LoadAs<T> (string relativePath) {
			lock(relativePath)
			{
				return RetryLoadAs<T>(Path.Combine(dataPaht,relativePath));
			}
		}

		/// <summary>
		/// Retries the loading if seralization fails (currupted data)
		/// </summary>
		/// <param name="absolutePath"></param>
		/// <param name="attempt">The number of attempts left</param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		private static T RetryLoadAs<T>(string absolutePath, short attempt = 2)
		{
			try{
				using(var f = File.OpenRead(absolutePath))
				{
					if(f.Length == 0)
					{
						throw new Exception("Empty File");
					}
					return Deserialize<T> (f);
				}
			}
			catch(Exception e)
			{
				Console.WriteLine($"Loaderror {absolutePath} {e.Message}");
				var info = new FileInfo(absolutePath);
					Console.WriteLine(info.LastAccessTimeUtc);
					Console.WriteLine(info.LastWriteTimeUtc);
					Console.WriteLine(info.Length);


				if(attempt <= 0 )
					throw e;
				else
				{
					// wait
					Thread.Sleep(1000);

					

					return RetryLoadAs<T>(absolutePath,(short)(attempt-1));
				}
			}
		}

		private static T Deserialize<T> (byte[] data) {
			return MessagePackSerializer.Deserialize<T> (data);
		}

		private static T Deserialize<T> (Stream data) {
			return MessagePackSerializer.Deserialize<T> (data);
		}

		/// <summary>
		/// Serializes and saves some data
		/// </summary>
		/// <param name="relativePath">Relative path.</param>
		/// <param name="data">Data.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static void SaveAs<T> (string relativePath, T data) {
			lock(relativePath)
			{
				WriteAllBytes (relativePath, MessagePackSerializer.Serialize (data));
			}
		}

		/// <summary>
		/// Delete the  file at specified relativePath.
		/// </summary>
		/// <param name="relativePath">Relative path.</param>
		public static void Delete (string relativePath) {
			var path = Path.Combine (dataPaht, relativePath);
			if (Directory.Exists (Path.GetDirectoryName (path)))
				File.Delete (path);
		}

		/// <summary>
		/// Deletes the relative folder with everything in it
		/// </summary>
		/// <param name="relativePath">Relative path to the folder</param>
		public static void DeleteFolder (string relativePath) {
			var path = Path.Combine (dataPaht, relativePath);
			if (Directory.Exists (path)) {
				Directory.Delete (path, true);
			}
		}

		/// <summary>
		/// Move the specified relavtiveOrigin to relativeDestination.
		/// </summary>
		/// <param name="relavtiveOrigin">Relavtive origin.</param>
		/// <param name="relativeDestination">Relative destination.</param>
		public static void Move (string relavtiveOrigin, string relativeDestination, bool overwrite = false) {
			var destination = Path.Combine (dataPaht, relativeDestination);
			Directory.CreateDirectory(Path.GetDirectoryName(destination));
			if(File.Exists(destination) && overwrite)
			{
				File.Delete(destination);
			}
			File.Move (GetAbsolutePath( relavtiveOrigin), destination);
		}

		public static string GetAbsolutePath(string relativePath)
		{
			return Path.Combine (dataPaht, relativePath);
		} 

		/// <summary>
		/// Creates all the parent direcotrys to the path
		/// </summary>
		/// <param name="relativePath"></param>
		public static void CreatePath(string relativePath)
		{
			Directory.CreateDirectory (Path.GetDirectoryName (Path.Combine (dataPaht, relativePath)));
		}
	}
}