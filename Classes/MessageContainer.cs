﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptLink {
    public class MessageContainer : Hashable {
        public Hash SenderHash { get; set; }
        public Hash ReceiverHash { get; set; }
        public Hash.HashProvider Provider { get; set; }
        public bool Encrypted { get; set; }
        public byte[] Payload { get; set; }

        static int intLength = sizeof(int);

        public MessageContainer(Hash Sender, Hash Receiver, Hash.HashProvider HashProvider) {
            SenderHash = Sender;
            ReceiverHash = Receiver;
            Provider = HashProvider;
            Payload = new byte[0];
        }

        public override byte[] HashableData() {
            return BitConverter.GetBytes((int)Provider)
                .Concat(BitConverter.GetBytes(Encrypted))
                .Concat(SenderHash.Bytes)
                .Concat(ReceiverHash.Bytes)
                .Concat(Payload)
                .ToArray();
        }

        public byte[] ToBinary() {
            return HashableData()
                .Concat(GetHash(Provider).Bytes)
                .ToArray();
        }

        public static int ByteCount(Hash.HashProvider ForProvider, int PayloadBytes, bool ZeroIndexed) {
            int pl = Hash.GetProviderByteLength(ForProvider);

            if (ZeroIndexed) {
                return intLength + PayloadBytes + (pl * 3);
            } else {
                return intLength + PayloadBytes + (pl * 3) + 1;
            }
            
        }

        /// <summary>
        /// Non-zero index byte length
        /// </summary>
        public int ByteLength(bool ZeroIndexed) {
            return ByteCount(Provider, Payload.Length, ZeroIndexed);
        }

        public override string ToString() {
            return "Sender: '" + SenderHash.ToString() + 
                "', Receiver: '" + ReceiverHash.ToString() +
                "', Payload length: " + Payload.Length.ToString() +
                "', Hash: '" + GetHash(Provider).ToString();
        }

        public static MessageContainer FromBinary(byte[] Blob, bool EnforceHashCheck) {

            //check basic validity
            if (Blob == null) {
                throw new ArgumentNullException("Blob must not be null");
            } else if (Blob.Length < 29) {
                //4 bytes for provider (int), (32 / 8) * 3 for sender, receiver and hash bits
                throw new ArgumentOutOfRangeException("Blob is too short for any hash provider");
            }

            Hash.HashProvider provider;
                
            //get the provider type
            int providerInt = BitConverter.ToInt32(Blob, 0);
            if (Enum.IsDefined(typeof(Hash.HashProvider), providerInt)) {
                provider = (Hash.HashProvider)providerInt;
            } else {
                throw new IndexOutOfRangeException("Invalid provider type");
            }

            int hashLength = Hash.GetProviderByteLength(provider);

            //check provider specific minimum length
            int minLength = ByteCount(provider, 0, false);
            if (Blob.Length < minLength) {
                throw new ArgumentOutOfRangeException("Blob is too short for specified hash provider");
            }
           
            byte[] sender = new byte[hashLength];
            byte[] receiver = new byte[hashLength];
            byte[] hash = new byte[hashLength];

            Array.Copy(Blob, intLength + 1, sender, 0, hashLength);
            Array.Copy(Blob, intLength + hashLength + 1, receiver, 0, hashLength);
            Array.Copy(Blob, Blob.Length - hashLength, hash, 0, hashLength);

            //parse out the hashes
            MessageContainer container = new MessageContainer(
                Hash.FromComputedBytes(sender, provider),
                Hash.FromComputedBytes(receiver, provider),
                provider
            );
            
            //get the payload
            int payloadStart = intLength + (hashLength * 2) + 1;
            int payloadLength = Blob.Length - (payloadStart + hashLength);

            if (payloadLength > 0) {
                container.Payload = new byte[payloadLength];
                Array.Copy(Blob, payloadStart, container.Payload, 0, payloadLength);
            }

            //verify hash
            var newHash = container.GetHash(container.Provider);
            var oldHash = Hash.FromComputedBytes(hash, provider);

            if (newHash == oldHash || EnforceHashCheck == false) {
                return container;
            } else {
                throw new FormatException("Provided hash was different than the actual hash, data is invalid or modified.");
            }

        }

    }
}
