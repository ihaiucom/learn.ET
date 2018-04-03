using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ETModel
{
    /// <summary>
    /// 如果是服务器：创建TcpListener监听，监听连接
    /// 如果是客户端：建立与服务器的连接，可以建立多个服务器的连接
    /// 服务也可：建立其他服务器的连接
    /// 当对方掉线时，将会把TChannel移除
    /// </summary>
	public sealed class TService: AService
	{
		private TcpListener acceptor;

		private readonly Dictionary<long, TChannel> idChannels = new Dictionary<long, TChannel>();
		
		/// <summary>
		/// 即可做client也可做server
		/// </summary>
		public TService(IPEndPoint ipEndPoint)
		{
			this.acceptor = new TcpListener(ipEndPoint);
			this.acceptor.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			this.acceptor.Server.NoDelay = true;
			this.acceptor.Start();
		}

		public TService()
		{
		}

		public override void Dispose()
		{
			if (this.acceptor == null)
			{
				return;
			}

			foreach (long id in this.idChannels.Keys.ToArray())
			{
				TChannel channel = this.idChannels[id];
				channel.Dispose();
			}
			this.acceptor.Stop();
			this.acceptor = null;
		}
		
		public override AChannel GetChannel(long id)
		{
			TChannel channel = null;
			this.idChannels.TryGetValue(id, out channel);
			return channel;
		}

        /// <summary>
        /// 等待客户端连接
        /// 由NetworkComponent启动检测连接，调这个方法
        /// </summary>
        /// <returns>The channel.</returns>
		public override async Task<AChannel> AcceptChannel()
		{
			if (this.acceptor == null)
			{
				throw new Exception("service construct must use host and port param");
			}
			TcpClient tcpClient = await this.acceptor.AcceptTcpClientAsync();
			TChannel channel = new TChannel(tcpClient, this);
			this.idChannels[channel.Id] = channel;
			return channel;
		}

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <returns>返回连接通道TChannel</returns>
		public override AChannel ConnectChannel(IPEndPoint ipEndPoint)
		{
			TcpClient tcpClient = new TcpClient();
			TChannel channel = new TChannel(tcpClient, ipEndPoint, this);
			this.idChannels[channel.Id] = channel;

			return channel;
		}

		public override void Remove(long id)
		{
			TChannel channel;
			if (!this.idChannels.TryGetValue(id, out channel))
			{
				return;
			}
			if (channel == null)
			{
				return;
			}
			this.idChannels.Remove(id);
			channel.Dispose();
		}

		public override void Update()
		{
		}
	}
}