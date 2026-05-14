namespace App.Domain.Dashboard
{
    public sealed class UserSummary
    {
        public long Id { get; }
        public string Nickname { get; }
        public string ProfileImageUrl { get; }
        public bool Mutual { get; }
        public bool Following { get; }
        public bool Blocked { get; }
        public long? SpaceId { get; }

        public UserSummary(
            long id,
            string nickname,
            string profileImageUrl = null,
            bool mutual = false,
            bool following = false,
            bool blocked = false,
            long? spaceId = null)
        {
            Id = id;
            Nickname = nickname ?? string.Empty;
            ProfileImageUrl = profileImageUrl;
            Mutual = mutual;
            Following = following;
            Blocked = blocked;
            SpaceId = spaceId;
        }
    }
}
