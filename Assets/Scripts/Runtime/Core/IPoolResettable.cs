namespace FPS
{
    /// <summary>
    /// Component cần reset khi zombie reuse từ pool.
    /// ZombiePoolManager tự động gọi ResetForPool() cho tất cả implementations.
    /// </summary>
    public interface IPoolResettable
    {
        void ResetForPool();
    }
}
