using System;
using System.Threading.Tasks;

namespace ETModel
{
    /// <summary>
    /// 交给ActorMessageDispatherComponent派发的处理器
    /// 处理器需要添加 [ActorMessageHandlerAttribute]
    /// 
    /// 派生类： AMActorHandler
    /// </summary>
	public interface IMActorHandler
	{
		Task Handle(Session session, Entity entity, IActorMessage actorRequest);
		Type GetMessageType();
	}
}