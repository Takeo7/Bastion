namespace Bastion.Client
{
    /// <summary>
    /// What moves in a strip of captures (Bastion.EditorTools.Shots): the scene is stepped by hand, a fixed time a
    /// frame, so a strip comes out the same every time. Animators and particle systems are stepped by the tool
    /// itself; anything else that moves with time takes its step here.
    /// </summary>
    public interface IShotStep
    {
        void ShotStep(float dt);
    }
}
