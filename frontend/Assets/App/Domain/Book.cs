namespace App.Domain
{
    public sealed class Book
    {
        public string BookId { get; }
        public string Title { get; }
        public string Author { get; }
        public string Publisher { get; }
        public string Description { get; }
        public string ThumbnailUrl { get; }

        public Book(string bookId, string title, string author, string publisher, string description, string thumbnailUrl)
        {
            BookId = bookId;
            Title = title;
            Author = author;
            Publisher = publisher;
            Description = description;
            ThumbnailUrl = thumbnailUrl;
        }
    }
}

