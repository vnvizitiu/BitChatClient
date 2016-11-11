﻿/*
Technitium Bit Chat
Copyright (C) 2016  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using BitChatCore.Network;
using System;
using System.IO;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using TechnitiumLibrary.IO;

namespace BitChatCore.FileSharing
{
    public class SharedFileMetaData : IWriteStream
    {
        #region variables

        static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        string _fileName;
        ContentType _contentType;
        DateTime _lastModified;

        long _fileSize;
        int _blockSize;

        string _hashAlgo;
        byte[][] _blockHash;

        BinaryID _fileID;

        #endregion

        #region constructor

        public SharedFileMetaData(string fileName, ContentType contentType, DateTime lastModified, long fileSize, int blockSize, string hashAlgo, byte[][] blockHash)
        {
            _fileName = fileName;
            _contentType = contentType;
            _lastModified = lastModified;

            _fileSize = fileSize;
            _blockSize = blockSize;

            _hashAlgo = hashAlgo;
            _blockHash = blockHash;

            _fileID = ComputeFileID();
        }

        public SharedFileMetaData(Stream s)
        {
            BinaryReader bR = new BinaryReader(s);

            switch (bR.ReadByte()) //version
            {
                case 1:
                    _fileName = Encoding.UTF8.GetString(bR.ReadBytes(bR.ReadByte()));
                    _contentType = new ContentType(Encoding.UTF8.GetString(bR.ReadBytes(bR.ReadByte())));
                    _lastModified = _epoch.AddSeconds(bR.ReadInt64());

                    _fileSize = bR.ReadInt64();
                    _blockSize = bR.ReadInt32();

                    _hashAlgo = Encoding.ASCII.GetString(bR.ReadBytes(bR.ReadByte()));

                    int totalBlocks = Convert.ToInt32(Math.Ceiling(Convert.ToDouble((double)_fileSize / _blockSize)));

                    _blockHash = new byte[totalBlocks][];

                    int hashLength = bR.ReadByte();

                    for (int i = 0; i < totalBlocks; i++)
                        _blockHash[i] = bR.ReadBytes(hashLength);

                    _fileID = ComputeFileID();
                    break;

                default:
                    throw new BitChatException("FileMetaData format version not supported.");
            }
        }

        #endregion

        #region private

        private BinaryID ComputeFileID()
        {
            using (MemoryStream mS = new MemoryStream(_blockHash[0].Length * _blockHash.Length))
            {
                for (int i = 0; i < _blockHash.Length; i++)
                    mS.Write(_blockHash[i], 0, _blockHash[i].Length);

                mS.Position = 0;

                using (HashAlgorithm hash = HashAlgorithm.Create(_hashAlgo))
                {
                    return new BinaryID(hash.ComputeHash(mS));
                }
            }
        }

        #endregion

        #region public

        public byte[] ComputeBlockHash(byte[] blockData)
        {
            using (HashAlgorithm hash = HashAlgorithm.Create(_hashAlgo))
            {
                return hash.ComputeHash(blockData);
            }
        }

        public void WriteTo(Stream s)
        {
            BinaryWriter bW = new BinaryWriter(s);

            byte[] buffer = null;

            bW.Write((byte)1);

            buffer = Encoding.UTF8.GetBytes(_fileName);
            bW.Write(Convert.ToByte(buffer.Length));
            bW.Write(buffer);

            buffer = Encoding.UTF8.GetBytes(_contentType.MediaType);
            bW.Write(Convert.ToByte(buffer.Length));
            bW.Write(buffer);

            bW.Write(Convert.ToInt64((_lastModified - _epoch).TotalSeconds));

            bW.Write(_fileSize);
            bW.Write(_blockSize);

            buffer = Encoding.ASCII.GetBytes(_hashAlgo);
            bW.Write(Convert.ToByte(buffer.Length));
            bW.Write(buffer);

            bW.Write(Convert.ToByte(_blockHash[0].Length));

            for (int i = 0; i < _blockHash.Length; i++)
                bW.Write(_blockHash[i]);

            bW.Flush();
        }

        public byte[] ToArray()
        {
            using (MemoryStream mS = new MemoryStream())
            {
                WriteTo(mS);
                return mS.ToArray();
            }
        }

        public Stream ToStream()
        {
            MemoryStream mS = new MemoryStream();
            WriteTo(mS);
            mS.Position = 0;
            return mS;
        }

        #endregion

        #region properties

        public string FileName
        { get { return _fileName; } }

        public ContentType ContentType
        { get { return _contentType; } }

        public DateTime LastModified
        { get { return _lastModified; } }

        public long FileSize
        { get { return _fileSize; } }

        public int BlockSize
        { get { return _blockSize; } }

        public string HashAlgo
        { get { return _hashAlgo; } }

        public byte[][] BlockHash
        { get { return _blockHash; } }

        public BinaryID FileID
        { get { return _fileID; } }

        #endregion
    }
}
