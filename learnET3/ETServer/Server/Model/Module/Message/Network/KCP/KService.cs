using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ETModel
{
	public static class KcpProtocalType
	{
        // 连接请求
		public const uint SYN = 1;
		public const uint ACK = 2;
        // 断开连接
		public const uint FIN = 3;
	}


    /// <summary>
    /// C创建与服务器的连接
    /// S接收终端连接
    /// 接收消息处理
    /// Update KChannel自己添加到更新队列里的通道
    /// </summary>
	public sealed class KService : AService
	{
		private uint IdGenerater = 1000;

		public uint TimeNow { get; set; }

		private UdpClient socket;

		private readonly Dictionary<long, KChannel> idChannels = new Dictionary<long, KChannel>();

		private TaskCompletionSource<AChannel> acceptTcs;

		private readonly Queue<long> removedChannels = new Queue<long>();

		// 下帧要更新的channel
		private readonly HashSet<long> updateChannels = new HashSet<long>();

		// 下次时间更新的channel <time, channelId>
		private readonly MultiMap<long, long> timerId = new MultiMap<long, long>();

		private readonly List<long> timeOutId = new List<long>();

        /// <summary>
        /// 客户端                                                                                                                          
        /// </summary>
		public KService(IPEndPoint ipEndPoint)
		{
			this.TimeNow = (uint)TimeHelper.Now();
			this.socket = new UdpClient(ipEndPoint);

#if SERVER
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				const uint IOC_IN = 0x80000000;
				const uint IOC_VENDOR = 0x18000000;
				uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
				this.socket.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
			}
#endif

			this.StartRecv();
		}

        /// <summary>
        /// 服务器
        /// </summary>
		public KService()
		{
			this.TimeNow = (uint)TimeHelper.Now();
			this.socket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
			this.StartRecv();
		}

		public override void Dispose()
		{
			if (this.socket == null)
			{
				return;
			}

			this.socket.Close();
			this.socket = null;
		}

		public async void StartRecv()
		{
			while (true)
			{
				if (this.socket == null)
				{
					return;
				}

                // 接收终端连接
				UdpReceiveResult udpReceiveResult;
				try
				{
					udpReceiveResult = await this.socket.ReceiveAsync();
				}
				catch (Exception e)
				{
					Log.Error(e);
					continue;
				}

                // 接收终端消息
				try
				{
					int messageLength = udpReceiveResult.Buffer.Length;

					// 长度小于4，不是正常的消息
					if (messageLength < 4)
					{
						continue;
					}

					// accept
					uint conn = BitConverter.ToUInt32(udpReceiveResult.Buffer, 0);

					// conn从1000开始，如果为1，2，3则是特殊包
					switch (conn)
					{
                        // 连接请求
						case KcpProtocalType.SYN:
							// 长度!=8，不是accpet消息
							if (messageLength != 8)
							{
								break;
							}
							this.HandleAccept(udpReceiveResult);
							break;
                        // 被接收连接
						case KcpProtocalType.ACK:
							// 长度!=12，不是connect消息
							if (messageLength != 12)
							{
								break;
							}
							this.HandleConnect(udpReceiveResult);
							break;
                        // 断开连接
						case KcpProtocalType.FIN:
							// 长度!=12，不是DisConnect消息
							if (messageLength != 12)
							{
								break;
							}
							this.HandleDisConnect(udpReceiveResult);
							break;
						default:
							this.HandleRecv(udpReceiveResult, conn);
							break;
					}
				}
				catch (Exception e)
				{
					Log.Error(e);
					continue;
				}
			}
		}

        /// <summary>
        /// 客户端接受到，服务器被接受连接
        /// </summary>
        /// <param name="udpReceiveResult">UDP receive result.</param>
		private void HandleConnect(UdpReceiveResult udpReceiveResult)
		{
			uint requestConn = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);
			uint responseConn = BitConverter.ToUInt32(udpReceiveResult.Buffer, 8);

			KChannel kChannel;
			if (!this.idChannels.TryGetValue(requestConn, out kChannel))
			{
				return;
			}
			// 处理chanel
			kChannel.HandleConnnect(responseConn);
		}

        /// <summary>
        /// 接受到对方要断开连接
        /// </summary>
		private void HandleDisConnect(UdpReceiveResult udpReceiveResult)
		{
			uint requestConn = BitConverter.ToUInt32(udpReceiveResult.Buffer, 8);

			KChannel kChannel;
			if (!this.idChannels.TryGetValue(requestConn, out kChannel))
			{
				return;
			}
			// 处理chanel
			this.idChannels.Remove(requestConn);
			kChannel.Dispose();
		}

        /// <summary>
        /// 接受消息
        /// </summary>
		private void HandleRecv(UdpReceiveResult udpReceiveResult, uint conn)
		{
			KChannel kChannel;
			if (!this.idChannels.TryGetValue(conn, out kChannel))
			{
				return;
			}
			// 处理chanel
			kChannel.HandleRecv(udpReceiveResult.Buffer, this.TimeNow);
		}

        /// <summary>
        /// 处理连接请求
        /// </summary>
		private void HandleAccept(UdpReceiveResult udpReceiveResult)
		{
			if (this.acceptTcs == null)
			{
				return;
			}

			uint requestConn = BitConverter.ToUInt32(udpReceiveResult.Buffer, 4);

			// 如果已经连接上,则重新响应请求
			KChannel kChannel;
			if (this.idChannels.TryGetValue(requestConn, out kChannel))
			{
				kChannel.HandleAccept(requestConn);
				return;
			}

			TaskCompletionSource<AChannel> t = this.acceptTcs;
			this.acceptTcs = null;
			kChannel = this.CreateAcceptChannel(udpReceiveResult.RemoteEndPoint, requestConn);
			kChannel.HandleAccept(requestConn);
			t.SetResult(kChannel);
		}

        /// <summary>
        /// 创建新连接的终端通道
        /// </summary>
		private KChannel CreateAcceptChannel(IPEndPoint remoteEndPoint, uint remoteConn)
		{
			KChannel channel = new KChannel(++this.IdGenerater, remoteConn, this.socket, remoteEndPoint, this);
			KChannel oldChannel;
			if (this.idChannels.TryGetValue(channel.Id, out oldChannel))
			{
				this.idChannels.Remove(oldChannel.Id);
				oldChannel.Dispose();
			}
			this.idChannels[channel.Id] = channel;
			return channel;
		}

        /// <summary>
        /// 创建与服务器连接
        /// </summary>
		private KChannel CreateConnectChannel(IPEndPoint remoteEndPoint)
		{
			uint conv = (uint)RandomHelper.RandomNumber(1000, int.MaxValue);
			KChannel channel = new KChannel(conv, this.socket, remoteEndPoint, this);
			KChannel oldChannel;
			if (this.idChannels.TryGetValue(channel.Id, out oldChannel))
			{
				this.idChannels.Remove(oldChannel.Id);
				oldChannel.Dispose();
			}
			this.idChannels[channel.Id] = channel;
			return channel;
		}

		public void AddToUpdate(long id)
		{
			this.updateChannels.Add(id);
		}

		public void AddToNextTimeUpdate(long time, long id)
		{
			this.timerId.Add(time, id);
		}

		public override AChannel GetChannel(long id)
		{
			KChannel channel;
			this.idChannels.TryGetValue(id, out channel);
			return channel;
		}

		public override Task<AChannel> AcceptChannel()
		{
			acceptTcs = new TaskCompletionSource<AChannel>();
			return this.acceptTcs.Task;
		}

        /// <summary>
        /// 连接服务器
        /// </summary>
		public override AChannel ConnectChannel(IPEndPoint ipEndPoint)
		{
			KChannel channel = this.CreateConnectChannel(ipEndPoint);
			return channel;
		}


		public override void Remove(long id)
		{
			KChannel channel;
			if (!this.idChannels.TryGetValue(id, out channel))
			{
				return;
			}
			if (channel == null)
			{
				return;
			}
			this.removedChannels.Enqueue(id);
			channel.Dispose();
		}

		public override void Update()
		{
			this.TimerOut();

			foreach (long id in updateChannels)
			{
				KChannel kChannel;
				if (!this.idChannels.TryGetValue(id, out kChannel))
				{
					continue;
				}
				if (kChannel.Id == 0)
				{
					continue;
				}
				kChannel.Update(this.TimeNow);
			}
			this.updateChannels.Clear();

			while (true)
			{
				if (this.removedChannels.Count <= 0)
				{
					break;
				}
				long id = this.removedChannels.Dequeue();
				this.idChannels.Remove(id);
			}
		}

		// 计算到期需要update的channel
		private void TimerOut()
		{
			if (this.timerId.Count == 0)
			{
				return;
			}

			this.TimeNow = (uint)TimeHelper.ClientNow();

			timeOutId.Clear();

			while (this.timerId.Count > 0)
			{
				long k = this.timerId.FirstKey();
				if (k > this.TimeNow)
				{
					break;
				}
				foreach (long ll in this.timerId[k])
				{
					this.timeOutId.Add(ll);
				}
				this.timerId.Remove(k);
			}

			foreach (long k in this.timeOutId)
			{
				this.updateChannels.Add(k);
			}
		}
	}
}