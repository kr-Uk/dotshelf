namespace App.Domain
{
    public enum ReadingStatus
    {
        Wishlist,
        Reading,
        Completed
    }

    public enum Visibility
    {
        Public,
        Private
    }

    public sealed class UserBook
    {
        public string UserBookId { get; }
        public string BookId { get; }
        public ReadingStatus Status { get; set; }
        public int Rating { get; set; }
        public string OneLineReview { get; set; }
        public Visibility ReviewVisibility { get; set; } = Visibility.Public;

        public UserBook(string userBookId, string bookId)
        {
            UserBookId = userBookId;
            BookId = bookId;
        }
    }
}

