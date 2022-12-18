using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FarNet;
using FarNet.Tools;

namespace MultiHasher
{
	[ModuleHost(Load = false)]
	public class MultiHasherHost : ModuleHost
	{
		public static IListMenu HashMenu;

		public override void Connect()
		{
			Dictionary<string, Func<HashAlgorithm>> hashAlgorithms = new Dictionary<string, Func<HashAlgorithm>>()
			{
				{ "CRC32", () => new CRC32() },
				{ "MD5", () => MD5.Create() },
				{ "SHA1", () => SHA1.Create() },
				{ "SHA256", () => SHA256.Create() },
				{ "SHA512", () => SHA512.Create() },
			};

			HashMenu = Far.Api.CreateListMenu();

			foreach (var hash in hashAlgorithms)
			{
				HashMenu.Add(hash.Key, HashMenuHandler).Data = hash.Value;
			}
			HashMenu.Title = "Select Hash Algorythm";
		}

		public override void Disconnect()
		{
		}

		private static void HashMenuHandler(object sender, EventArgs e)
		{
			IAnyMenu menu = (sender as IAnyMenu);
			FarItem menuItem = menu.Items[menu.Selected];

			CalculateHash(
				menuItem.Text,
				menuItem.Data as Func<HashAlgorithm>
			);
		}

		class CalculationContext
		{
			public string HashName { get; set; }
			public Func<HashAlgorithm> Hash { get; set; }
			public List<string> Files { get; set; }
			public Dictionary<string, string> Result { get; private set; }

			public CalculationContext()
			{
				Result = new Dictionary<string, string>();
			}
		}

		private static void CalculateHash(string hashName, Func<HashAlgorithm> hash)
		{
			IPanel activePanel = Far.Api.Panel;
			if (activePanel == null)
			{
				return;
			}

			List<string> files = new List<string>();
			IViewer viewer = Far.Api.Viewer;
			if (viewer == null)
			{
				foreach (var file in activePanel.SelectedFiles)
				{
					files.Add(activePanel.CurrentDirectory + "\\" + file.Name);
				}
				if (files.Count == 0)
				{
					return;
				}
			}
			else
			{
				files.Add(viewer.FileName);
			}

			CalculationContext context = new CalculationContext { Files = files, HashName = hashName, Hash = hash };
			ProgressForm progress = new ProgressForm()
			{
				Title = "Computing",
				CanCancel = true,
			};

			if (!progress.Show(Task.Run(() => DoHashCalculation(context, progress))))
			{
				return;
			}

			Dictionary<string, string> hashBlobs = context.Result;

			if (hashBlobs.Count == 0)
			{
				return;
			}

			bool saveToFile;
			if (hashBlobs.Count == 1)
			{
				var enumerator = hashBlobs.GetEnumerator();
				enumerator.MoveNext();
				KeyValuePair<string, string> hashBlob = enumerator.Current;
				saveToFile = Far.Api.Message(
					hashBlob.Value,
					hashBlob.Key,
					MessageOptions.None,
					new[] { "Close", "Save to file" }
				) == 1;
			}
			else
			{
				saveToFile = true;
			}

			if (saveToFile)
			{
				SaveHashesToFile(hashName, hashBlobs);
			}
		}

		private static string CalculateFileHash(AsyncStreamHashAlgorithm hashing, string fileName)
		{
			using (Stream s = new FileStream(fileName, FileMode.Open, FileAccess.Read))
			{
				hashing.ComputeHash(s);
			}
			return hashing.ToString();
		}

		private static void SaveHashesToFile(string hashName, Dictionary<string, string> hashBlobs)
		{
			string path = Far.Api.CurrentDirectory + "\\";
			if (hashBlobs.Count > 1)
			{
				path += "CHECKSUM." + hashName;
			}
			else
			{
				foreach (var hashBlob in hashBlobs)
				{
					path += hashBlob.Key + "." + hashName;
				}
			}

			using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				using (TextWriter writer = new StreamWriter(file))
				{
					foreach (var hashBlob in hashBlobs)
					{
						writer.WriteLine("{0} ({1}) = {2}", hashName, hashBlob.Key, hashBlob.Value);
					}
				}
			}
		}

		private static void DoHashCalculation(CalculationContext context, ProgressForm progress)
		{
			long totalSize = 0;
			long processedSize = 0;

			foreach (var fileName in context.Files)
			{
				if (File.Exists(fileName))
				{
					totalSize += new FileInfo(fileName).Length;
				}
			}

			foreach (var fileName in context.Files)
			{
				if (File.Exists(fileName))
				{
					AsyncStreamHashAlgorithm hashing = new AsyncStreamHashAlgorithm(context.Hash());
					hashing.FileHashingProgress += (s, e) =>
					{
						progress.Activity = Path.GetFileName(fileName);
						progress.SetProgressValue(e.TotalBytesRead + processedSize, totalSize);
					};

					context.Result.Add(
						Path.GetFileName(fileName),
						CalculateFileHash(hashing, fileName)
					);

					processedSize += new FileInfo(fileName).Length;
				}
			}
		}
	}
}
