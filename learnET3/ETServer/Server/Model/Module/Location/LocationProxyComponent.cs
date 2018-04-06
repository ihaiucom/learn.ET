﻿using System.Net;
using System.Threading.Tasks;

namespace ETModel
{
	[ObjectSystem]
	public class LocationProxyComponentSystem : AwakeSystem<LocationProxyComponent>
	{
		public override void Awake(LocationProxyComponent self)
		{
			self.Awake();
		}
	}

    /// <summary>
    /// 非Location服的 代理，负责ActorComponent向LocationComponent的注册Add、查找Get、移除Remove、锁Lock、解锁UnLock
    /// </summary>
	public class LocationProxyComponent : Component
	{
		public IPEndPoint LocationAddress;

		public int AppId;

		public void Awake()
		{
			StartConfigComponent startConfigComponent = Game.Scene.GetComponent<StartConfigComponent>();
			this.AppId = startConfigComponent.StartConfig.AppId;

			StartConfig startConfig = startConfigComponent.LocationConfig;
			this.LocationAddress = startConfig.GetComponent<InnerConfig>().IPEndPoint;
		}

		public async Task Add(long key)
		{
			Session session = Game.Scene.GetComponent<NetInnerComponent>().Get(this.LocationAddress);
			await session.Call(new ObjectAddRequest() { Key = key, AppId = this.AppId });
		}

		public async Task Lock(long key, int time = 1000)
		{
			Session session = Game.Scene.GetComponent<NetInnerComponent>().Get(this.LocationAddress);
			await session.Call(new ObjectLockRequest() { Key = key, LockAppId = this.AppId, Time = time });
		}

		public async Task UnLock(long key, int value)
		{
			Session session = Game.Scene.GetComponent<NetInnerComponent>().Get(this.LocationAddress);
			await session.Call(new ObjectUnLockRequest() { Key = key, UnLockAppId = this.AppId, AppId = value});
		}

		public async Task Remove(long key)
		{
			Session session = Game.Scene.GetComponent<NetInnerComponent>().Get(this.LocationAddress);
			await session.Call(new ObjectRemoveRequest() { Key = key });
		}

		public async Task<int> Get(long key)
		{
			Session session = Game.Scene.GetComponent<NetInnerComponent>().Get(this.LocationAddress);
			ObjectGetResponse response = (ObjectGetResponse)await session.Call(new ObjectGetRequest() { Key = key });
			return response.AppId;
		}

		public override void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}
			base.Dispose();
		}
	}
}