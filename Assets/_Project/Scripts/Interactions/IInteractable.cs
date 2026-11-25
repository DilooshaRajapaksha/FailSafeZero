namespace FailSafeZero.Interactions
{
    
    public interface IInteractable
    {
        string Prompt { get; }

        void Interact(PlayerInteractor interactor);
    }
}
