using System;
using App.Domain;
using UnityEngine;

namespace App.Features.BookViewer
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BookView : MonoBehaviour
    {
        [SerializeField] private string userBookId;
        [SerializeField] private BookState currentState = BookState.Idle;

        public string UserBookId => userBookId;
        public UserBook UserBook { get; private set; }
        public Book BookMeta { get; private set; }
        public BookPlacement Placement { get; private set; }
        public BookState CurrentState => currentState;

        public event Action<BookView, BookState, BookState> OnStateChanged;

        public void Bind(UserBook userBook, Book bookMeta, BookPlacement placement)
        {
            UserBook = userBook;
            BookMeta = bookMeta;
            Placement = placement;
            userBookId = userBook != null ? userBook.UserBookId : string.Empty;
        }

        public void SetState(BookState next)
        {
            if (currentState == next)
            {
                return;
            }

            var previous = currentState;
            currentState = next;
            OnStateChanged?.Invoke(this, previous, next);
        }

        public void SetSpineInfoVisible(bool visible)
        {
            // Phase 3+: hook TMP spine labels here.
        }
    }
}

