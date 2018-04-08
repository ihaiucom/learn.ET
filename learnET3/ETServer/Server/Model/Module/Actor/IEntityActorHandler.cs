using System.Threading.Tasks;

namespace ETModel
{
    /// <summary>
    /// ActorComponent.entityActorHandler 挂载上的处理器
    /// </summary>
	public interface IEntityActorHandler
	{
		Task Handle(Session session, Entity entity, IActorMessage actorMessage);
	}
}