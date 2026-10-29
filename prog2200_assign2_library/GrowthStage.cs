namespace prog2200_assign2_library
{
    /// <summary>
    /// The life cycle every flower goes through. After <see cref="Wilt"/> the flower drops a new seed
    /// and starts again at <see cref="Seed"/>.
    /// </summary>
    public enum GrowthStage
    {
        Seed,
        Sprout,
        Bud,
        Bloom,
        Wilt
    }
}
