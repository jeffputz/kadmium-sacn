﻿using Kadmium_sACN.MulticastAddressProvider;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kadmium_sACN.SacnSender
{
	public class SacnSender : IDisposable
	{
		private ISacnMulticastAddressProvider Ipv4MulticastAddressProvider { get; }
		private ISacnMulticastAddressProvider Ipv6MulticastAddressProvider { get; }
		private Socket Ipv4Socket { get; } = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		private Socket Ipv6Socket { get; } = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
		
		private IPAddress _ip;

		public SacnSender() : this(new SacnMulticastAddressProviderIPV4(), new SacnMulticastAddressProviderIPV6())
		{ }

		public SacnSender(IPAddress localAddress) : this(new SacnMulticastAddressProviderIPV4(),
			new SacnMulticastAddressProviderIPV6())
		{
			_ip = localAddress;
		}

		protected SacnSender(ISacnMulticastAddressProvider ipv4AddressProvider, ISacnMulticastAddressProvider ipv6AddressProvider)
		{
			Ipv4MulticastAddressProvider = ipv4AddressProvider;
			Ipv6MulticastAddressProvider = ipv6AddressProvider;
		}

		protected async Task SendInternal(IPAddress address, SacnPacket packet)
		{
			var endpoint = new IPEndPoint(address, Constants.RemotePort);
			using (var owner = MemoryPool<byte>.Shared.Rent(packet.Length))
			{
				var bytes = owner.Memory.Slice(0, packet.Length);
				packet.Write(bytes.Span);
				var socket = address.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6Socket : Ipv4Socket;
				
				if (_ip != null)
					socket.Bind(new IPEndPoint(_ip, Constants.LocalPort));
				
				var args = new SocketAsyncEventArgs
				{
					SocketFlags = SocketFlags.None,
					RemoteEndPoint = endpoint
				};
				args.SetBuffer(bytes);
				var tsc = new TaskCompletionSource<SocketAsyncEventArgs>();
				args.Completed += (_, args) =>
				{
					tsc.SetResult(args);
				};
				bool result = socket.SendToAsync(args);
				if (result)
				{
					await tsc.Task;
				}

			}
		}

		public Task SendUnicast(DataPacket packet, IPAddress remoteHost)
		{
			return SendInternal(remoteHost, packet);
		}

		public Task SendUnicast(UniverseDiscoveryPacket packet, IPAddress remoteHost)
		{
			return SendInternal(remoteHost, packet);
		}

		public Task SendUnicast(SynchronizationPacket packet, IPAddress remoteHost)
		{
			return SendInternal(remoteHost, packet);
		}

		public Task SendMulticast(DataPacket packet, bool ipv6 = false)
		{
			if (!ipv6)
				return SendInternal(Ipv4MulticastAddressProvider.GetMulticastAddress(packet.FramingLayer.Universe), packet);
			return SendInternal(Ipv6MulticastAddressProvider.GetMulticastAddress(packet.FramingLayer.Universe), packet);
		}

		public Task SendMulticast(UniverseDiscoveryPacket packet, bool ipv6 = false)
		{
			if (!ipv6)
				return SendInternal(Ipv4MulticastAddressProvider.GetMulticastAddress(UniverseDiscoveryPacket.DiscoveryUniverse), packet);
			return SendInternal(Ipv6MulticastAddressProvider.GetMulticastAddress(UniverseDiscoveryPacket.DiscoveryUniverse), packet);
		}

		public Task SendMulticast(SynchronizationPacket packet, bool ipv6 = false)
		{
			if (!ipv6)
				return SendInternal(Ipv4MulticastAddressProvider.GetMulticastAddress(packet.FramingLayer.SynchronizationAddress), packet);
			return SendInternal(Ipv6MulticastAddressProvider.GetMulticastAddress(packet.FramingLayer.SynchronizationAddress), packet);
		}

		public void Dispose()
		{
			Ipv4Socket.Dispose();
			Ipv6Socket.Dispose();
		}
	}
}
