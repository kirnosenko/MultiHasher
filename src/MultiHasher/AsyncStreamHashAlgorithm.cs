using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MultiHasher
{
	public class AsyncStreamHashAlgorithm
	{
		protected readonly HashAlgorithm _hashAlgorithm;
		protected byte[] _hash;
		protected bool _cancel;
		protected int _bufferSize = 4096;
		public delegate void FileHashingProgressHandler(object sender, FileHashingProgressArgs e);
		public event FileHashingProgressHandler FileHashingProgress;

		public AsyncStreamHashAlgorithm(HashAlgorithm hashAlgorithm)
		{
			_hashAlgorithm = hashAlgorithm;
		}

		public byte[] ComputeHash(Stream stream)
		{
			_cancel = false;
			_hash = null;
			int bufferSize = _bufferSize; // this makes it impossible to change the buffer size while computing

			long totalBytesRead = 0;

			long size = stream.Length;
			byte[] readAheadBuffer = new byte[bufferSize];
			int readAheadBytesRead = stream.Read(readAheadBuffer, 0, readAheadBuffer.Length);

			totalBytesRead += readAheadBytesRead;

			do
			{
				int bytesRead = readAheadBytesRead;
				byte[] buffer = readAheadBuffer;

				readAheadBuffer = new byte[bufferSize];
				readAheadBytesRead = stream.Read(readAheadBuffer, 0, readAheadBuffer.Length);

				totalBytesRead += readAheadBytesRead;

				if (readAheadBytesRead == 0)
					_hashAlgorithm.TransformFinalBlock(buffer, 0, bytesRead);
				else
					_hashAlgorithm.TransformBlock(buffer, 0, bytesRead, buffer, 0);

				FileHashingProgress(this, new FileHashingProgressArgs(totalBytesRead, size));
			} while (readAheadBytesRead != 0 && !_cancel);

			if (_cancel)
			{
				return _hash = null;
			}

			return _hash = _hashAlgorithm.Hash;
		}

		public int BufferSize
		{
			get { return _bufferSize; }
			set { _bufferSize = value; }
		}

		public byte[] Hash
		{
			get { return _hash; }
		}

		public virtual void Cancel()
		{
			_cancel = true;
		}

		public override string ToString()
		{
			StringBuilder hashText = new StringBuilder();
			for (int i = 0; i < Hash.Length; i++)
			{
				hashText.Append(Hash[i].ToString("x2").ToLower());
			}
			return hashText.ToString();
		}
	}

	public class FileHashingProgressArgs : EventArgs
	{
		public long TotalBytesRead { get; set; }
		public long Size { get; set; }

		public FileHashingProgressArgs(long totalBytesRead, long size)
		{
			TotalBytesRead = totalBytesRead;
			Size = size;
		}
	}
}
