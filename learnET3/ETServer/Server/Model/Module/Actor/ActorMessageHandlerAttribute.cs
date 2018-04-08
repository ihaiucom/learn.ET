using System;

namespace ETModel
{

    /// <summary>
    /// 交给ActorMessageDispatherComponent派发的处理器，需要加的属性
    /// 处理器继承 AMActorHandler : IMActorHandler
    /// </summary>
	public class ActorMessageHandlerAttribute : Attribute
	{
		public AppType Type { get; }

		public ActorMessageHandlerAttribute(AppType appType)
		{
			this.Type = appType;
		}
	}
}