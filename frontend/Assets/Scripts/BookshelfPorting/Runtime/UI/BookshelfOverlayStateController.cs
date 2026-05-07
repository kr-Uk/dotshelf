namespace BookshelfPorting.Runtime
{
    internal readonly struct BookshelfOverlayStateSnapshot
    {
        public BookshelfOverlayStateSnapshot(
            bool isBookshelfNameEditPanelOpen,
            bool isBookshelfDeleteConfirmPanelOpen,
            bool isBookshelfDesignPanelOpen,
            bool isBookAddPanelOpen,
            bool isBookAddConfirmPanelOpen,
            bool isBookshelfManagePanelOpen,
            bool isBookshelfUnsavedConfirmPanelOpen,
            bool isDeleteResultDialogOpen,
            bool isBookSearchPanelOpen,
            bool isBookAddPlacementSelectionActive,
            bool isBookEditPanelOpen,
            bool isBookPlacementEditorOpen,
            bool isBookReportWritePanelOpen,
            bool isBookReportReadPanelOpen,
            bool isFirstBookshelfTutorialOpen,
            bool hasActiveEditSection)
        {
            IsBookshelfNameEditPanelOpen = isBookshelfNameEditPanelOpen;
            IsBookshelfDeleteConfirmPanelOpen = isBookshelfDeleteConfirmPanelOpen;
            IsBookshelfDesignPanelOpen = isBookshelfDesignPanelOpen;
            IsBookAddPanelOpen = isBookAddPanelOpen;
            IsBookAddConfirmPanelOpen = isBookAddConfirmPanelOpen;
            IsBookshelfManagePanelOpen = isBookshelfManagePanelOpen;
            IsBookshelfUnsavedConfirmPanelOpen = isBookshelfUnsavedConfirmPanelOpen;
            IsDeleteResultDialogOpen = isDeleteResultDialogOpen;
            IsBookSearchPanelOpen = isBookSearchPanelOpen;
            IsBookAddPlacementSelectionActive = isBookAddPlacementSelectionActive;
            IsBookEditPanelOpen = isBookEditPanelOpen;
            IsBookPlacementEditorOpen = isBookPlacementEditorOpen;
            IsBookReportWritePanelOpen = isBookReportWritePanelOpen;
            IsBookReportReadPanelOpen = isBookReportReadPanelOpen;
            IsFirstBookshelfTutorialOpen = isFirstBookshelfTutorialOpen;
            HasActiveEditSection = hasActiveEditSection;
        }

        public bool IsBookshelfNameEditPanelOpen { get; }
        public bool IsBookshelfDeleteConfirmPanelOpen { get; }
        public bool IsBookshelfDesignPanelOpen { get; }
        public bool IsBookAddPanelOpen { get; }
        public bool IsBookAddConfirmPanelOpen { get; }
        public bool IsBookshelfManagePanelOpen { get; }
        public bool IsBookshelfUnsavedConfirmPanelOpen { get; }
        public bool IsDeleteResultDialogOpen { get; }
        public bool IsBookSearchPanelOpen { get; }
        public bool IsBookAddPlacementSelectionActive { get; }
        public bool IsBookEditPanelOpen { get; }
        public bool IsBookPlacementEditorOpen { get; }
        public bool IsBookReportWritePanelOpen { get; }
        public bool IsBookReportReadPanelOpen { get; }
        public bool IsFirstBookshelfTutorialOpen { get; }
        public bool HasActiveEditSection { get; }
    }

    internal sealed class BookshelfOverlayStateController
    {
        public bool HasOpenBookshelfModal(BookshelfOverlayStateSnapshot state)
        {
            return state.IsBookshelfNameEditPanelOpen ||
                   state.IsBookshelfDeleteConfirmPanelOpen ||
                   state.IsBookshelfDesignPanelOpen ||
                   state.IsBookAddPanelOpen ||
                   state.IsBookAddConfirmPanelOpen ||
                   state.IsBookshelfManagePanelOpen ||
                   state.IsBookshelfUnsavedConfirmPanelOpen ||
                   state.IsDeleteResultDialogOpen ||
                   state.IsFirstBookshelfTutorialOpen;
        }

        public bool HasBlockingBookshelfOverlay(BookshelfOverlayStateSnapshot state)
        {
            return HasOpenBookshelfModal(state) ||
                   state.IsBookSearchPanelOpen ||
                   state.IsBookAddPlacementSelectionActive ||
                   state.IsBookEditPanelOpen ||
                   state.IsBookPlacementEditorOpen ||
                   state.IsBookReportWritePanelOpen ||
                   state.IsBookReportReadPanelOpen ||
                   state.HasActiveEditSection;
        }

        public bool CanHandleFrontalZoomInput(
            ExperienceFocusArea focusArea,
            bool isViewingBook,
            bool hasActiveDraggedBook,
            bool isPointerOverUi,
            BookshelfOverlayStateSnapshot state)
        {
            return focusArea == ExperienceFocusArea.Bookshelf &&
                   !isViewingBook &&
                   !hasActiveDraggedBook &&
                   !HasBlockingBookshelfOverlay(state) &&
                   !isPointerOverUi;
        }
    }
}
