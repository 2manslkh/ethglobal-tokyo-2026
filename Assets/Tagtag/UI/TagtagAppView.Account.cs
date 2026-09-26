using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            revealedWalletPhrase = null;
            walletRecoveryInput = "";
            if (walletPhraseLabel != null) walletPhraseLabel.text = "";
            if (walletRecoveryField != null) walletRecoveryField.SetValueWithoutNotify("");
            if (accountScreen == AccountScreen.Wallet) QueueRender();
        }
        private Label accountCollectionCount;
        private Label accountAuthoredCount;
        private VisualElement authoredListHost;
        private readonly PresenterCache authoredListContents = new PresenterCache();
        private readonly List<Button> authoredWithdrawButtons = new List<Button>();
        private Button appleSignInButton;
        private Button googleSignInButton;
        private Button deleteButton;
        private PaperField deleteField;
        private Label walletStatusLabel;
        private Button walletAddressButton;
        private Label walletPhraseLabel;
        private PaperField walletRecoveryField;
        private string revealedWalletPhrase;
        private string walletRecoveryInput = "";
        private string transferRecipient = "";
        private VisualElement nftTransferHost;
        private readonly PresenterCache nftTransferContents = new PresenterCache();

        private void BuildAccount(AppState state)
        {
            walletStatusLabel = null;
            walletAddressButton = null;
            VisualElement heading = Row(screenHost);
            heading.style.height = 62f;
            heading.style.paddingLeft = 16f;
            heading.style.paddingRight = 20f;
            heading.style.alignItems = Align.Center;
            Action(heading, "Back", () =>
            {
                if (accountScreen == AccountScreen.Overview || accountScreen == AccountScreen.SignIn)
                {
                    if (accountScreen == AccountScreen.SignIn && returnAfterSignIn != Sheet.None) RestoreSignInSheet();
                    controller.SetAccountOpen(false);
                }
                else
                {
                    revealedWalletPhrase = null;
                    walletRecoveryInput = "";
                    accountScreen = AccountScreen.Overview;
                    QueueRender();
                }
            }, false);
            Text(heading, accountScreen == AccountScreen.SignIn ? "Sign in" : accountScreen == AccountScreen.Authored ? "Your stickers" : accountScreen == AccountScreen.Wallet ? "Your wallet" : accountScreen == AccountScreen.DeleteConfirmation ? "Delete account" : "Account", 21, true);

            ScrollView scroll = PaperScroll(screenHost);
            scroll.name = "Account scroll";
            scroll.style.paddingLeft = 24f;
            scroll.style.paddingRight = 24f;
            VisualElement content = scroll.contentContainer;

            if (accountScreen == AccountScreen.Authored) BuildAuthored(content, state);
            else if (accountScreen == AccountScreen.Wallet) BuildWallet(content, state);
            else if (accountScreen == AccountScreen.DeleteConfirmation) BuildDeleteConfirmation(content, state);
            else BuildAccountOverview(content, state);
            AddStatus(content, state);
            RefreshAccount(state);
        }

        private void RefreshAccount(AppState state)
        {
            if (walletStatusLabel != null)
            {
                walletStatusLabel.text = NftPresentation.WalletStatus(state);
                walletStatusLabel.style.display = walletAddressButton != null && state.walletStatus == "ready"
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (walletAddressButton != null)
            {
                walletAddressButton.text = state.walletAddress;
                walletAddressButton.style.display = state.walletStatus == "ready" &&
                    !string.IsNullOrEmpty(NftPresentation.WalletExplorerUrl(state.walletAddress))
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RefreshNftTransferRows(state);
            if (accountCollectionCount != null) accountCollectionCount.text = CollectionPresentation.OrderedDistinct(state.collection).Count.ToString();
            if (accountAuthoredCount != null) accountAuthoredCount.text = state.authored.Count.ToString();
            if (deleteButton != null) SetDisabled(deleteButton, !NftPresentation.CanDelete(state, deleteConfirmation));
            if (appleSignInButton != null) SetDisabled(appleSignInButton, state.busy || !state.servicesConfigured);
            if (googleSignInButton != null) SetDisabled(googleSignInButton, state.busy || !state.servicesConfigured);
            foreach (Button button in authoredWithdrawButtons) SetDisabled(button, state.busy);
            if (authoredListHost != null) RefreshAuthored(state);
        }

        private void BuildAccountOverview(VisualElement content, AppState state)
        {
            Text(content, Safe(state.user.displayName, "Your account"), 28, true).style.marginTop = 22f;
            Text(content, "Your stickers and collection", 15, false, Muted).style.marginTop = 4f;
            if (state.nftEnabled)
            {
                Divider(content);
                Text(content, "Your souvenir wallet · Sepolia testnet", 17, true);
                BuildWalletAddress(content, state, 13);
                Text(content, "New discoveries become transferable NFTs. Tagtag covers minting; private notes stay in your book.", 14, false, Muted).style.marginTop = 6f;
                Action(content, "Back up or restore wallet", () =>
                {
                    accountScreen = AccountScreen.Wallet;
                    QueueRender();
                }, false).style.marginTop = 10f;
                if (state.walletStatus == "ready")
                    Text(content, "Write down your recovery words before changing phones or deleting this account.", 13, false, Muted).style.marginTop = 6f;
            }
            Divider(content);
            VisualElement collection = Row(content);
            collection.style.justifyContent = Justify.SpaceBetween;
            collection.style.alignItems = Align.Center;
            Text(collection, "Collected stickers", 15, false, Muted);
            accountCollectionCount = Text(collection, CollectionPresentation.OrderedDistinct(state.collection).Count.ToString(), 17, true);
            accountCollectionCount.userData = 24;
            accountCollectionCount.style.fontSize = Mathf.RoundToInt(24f * textScale);
            Action(content, "Open sticker book", () =>
            {
                controller.SetAccountOpen(false);
                controller.Navigate(AppPage.Home);
            }, false).style.alignSelf = Align.FlexStart;
            Divider(content);
            VisualElement authored = Row(content);
            authored.style.alignItems = Align.Center;
            authored.style.justifyContent = Justify.SpaceBetween;
            Text(authored, "Stickers you left", 17, true);
            accountAuthoredCount = Text(authored, state.authored.Count.ToString(), 17, false, Muted);
            Action(content, "Manage your stickers", () =>
            {
                accountScreen = AccountScreen.Authored;
                QueueRender();
            }, false).style.alignSelf = Align.FlexStart;
            Divider(content);
            Action(content, "Sign out", () =>
            {
                revealedWalletPhrase = null;
                walletRecoveryInput = "";
                controller.SignOut();
                accountScreen = AccountScreen.SignIn;
            }, false).style.alignSelf = Align.FlexStart;
            Action(content, "Delete account and stickers", () =>
            {
                deleteConfirmation = "";
                transferRecipient = "";
                (controller as INftTransferController)?.AcknowledgeNftLoss(false);
                accountScreen = AccountScreen.DeleteConfirmation;
                QueueRender();
            }, false).style.marginTop = 9f;
        }

        private void BuildWalletAddress(VisualElement content, AppState state, int fontSize)
        {
            walletStatusLabel = Text(content, NftPresentation.WalletStatus(state), fontSize, false, Muted);
            walletStatusLabel.style.marginTop = 6f;
            walletAddressButton = Action(content, state.walletAddress, () =>
            {
                string url = NftPresentation.WalletExplorerUrl(controller.State.walletAddress);
                if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
            }, false);
            walletAddressButton.name = "Wallet Etherscan address";
            walletAddressButton.tooltip = "View this address on Sepolia Etherscan";
            walletAddressButton.userData = fontSize;
            walletAddressButton.style.fontSize = Mathf.RoundToInt(fontSize * textScale);
            walletAddressButton.style.whiteSpace = WhiteSpace.Normal;
            walletAddressButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            walletAddressButton.style.alignSelf = Align.Stretch;
            walletAddressButton.style.flexShrink = 1f;
            walletAddressButton.style.paddingLeft = 10f;
            walletAddressButton.style.marginTop = 6f;
        }

        private void BuildWallet(VisualElement content, AppState state)
        {
            Text(content, "Your souvenir wallet", 27, true).style.marginTop = 22f;
            Text(content, "Sepolia testnet · NFTs are delivered to this address.", 14, false, Muted).style.marginTop = 5f;
            BuildWalletAddress(content, state, 14);
            if (state.walletStatus == "ready")
            {
                Text(content, "Your 12 recovery words are the only way to restore this wallet on another phone. Write them down privately and never share them. Anyone with the words can transfer your NFTs.", 15).style.marginTop = 18f;
                if (revealedWalletPhrase == null)
                {
                    Action(content, "Show recovery words", () =>
                    {
                        try { revealedWalletPhrase = (controller as IPhoneWalletController)?.RevealWalletPhrase(); }
                        catch { revealedWalletPhrase = null; }
                        QueueRender();
                    }, false).style.marginTop = 14f;
                }
                else
                {
                    walletPhraseLabel = Text(content, revealedWalletPhrase, 18, true);
                    walletPhraseLabel.style.marginTop = 14f;
                    Action(content, "Hide recovery words", () =>
                    {
                        revealedWalletPhrase = null;
                        QueueRender();
                    }, false).style.marginTop = 12f;
                }
            }
            if (state.walletStatus == "needsRecovery" || state.walletStatus == "delayed")
            {
                Text(content, "If you used this account on another phone, enter its 12 recovery words to receive NFTs at the same address. An existing NFT address cannot be replaced.", 15).style.marginTop = 18f;
                var field = new PaperField("Recovery words", walletRecoveryInput, 256, false,
                    "Enter all 12 words in order, separated by spaces");
                walletRecoveryField = field;
                field.name = "Wallet recovery words";
                field.isPasswordField = true;
                field.RegisterValueChangedCallback(evt => walletRecoveryInput = evt.newValue);
                content.Add(field);
                Action(content, "Restore wallet", () =>
                {
                    (controller as IPhoneWalletController)?.RestoreWalletPhrase(walletRecoveryInput);
                    walletRecoveryInput = "";
                    QueueRender();
                }).style.marginTop = 12f;
            }
        }

        private void BuildAuthored(VisualElement content, AppState state)
        {
            Text(content, "Stickers you left", 27, true).style.marginTop = 22f;
            Text(content, "Withdraw a sticker to stop new discoveries. Copies already collected remain in other books.",
                14, false, Muted).style.marginTop = 8f;
            authoredListHost = Column(content);
            authoredListContents.Reset();
            RefreshAuthored(state);
        }

        private void RefreshAuthored(AppState state)
        {
            if (authoredListHost == null) return;
            string key = state.user?.uid + ":" + state.authored.Count;
            foreach (StickerSummary item in state.authored)
                if (item != null) key += ":" + item.id + ":" + item.revision + ":" + item.designId + ":" + item.thumbnailUrl;
            if (!authoredListContents.NeedsRefresh(key)) return;
            authoredListHost.Clear();
            authoredWithdrawButtons.Clear();
            if (state.authored.Count == 0)
            {
                Text(authoredListHost, "You have not left a sticker yet.", 16, false, Muted).style.marginTop = 28f;
                Action(authoredListHost, "Open STICK", () =>
                {
                    controller.SetAccountOpen(false);
                    controller.Navigate(AppPage.Stick);
                }).style.marginTop = 15f;
                return;
            }
            foreach (StickerSummary sticker in state.authored)
            {
                if (sticker == null || string.IsNullOrEmpty(sticker.id)) continue;
                Divider(authoredListHost);
                VisualElement row = Row(authoredListHost);
                row.style.alignItems = Align.Center;
                Art(row, sticker, 62f);
                VisualElement words = Column(row);
                words.style.flexGrow = 1f;
                words.style.marginLeft = 12f;
                Text(words, Safe(sticker.place, "A place"), 17, true);
                Text(words, Safe(sticker.teaser, "No clue"), 13, false, Muted);
                Text(words, "Left " + Date(sticker.createdAt), 12, false, Muted);
                string stickerId = sticker.id;
                Button withdraw = Action(authoredListHost, "Withdraw this sticker", () =>
                {
                    sheet = Sheet.Withdraw;
                    sheetStickerId = stickerId;
                    QueueRender();
                }, false);
                withdraw.style.alignSelf = Align.FlexStart;
                authoredWithdrawButtons.Add(withdraw);
                SetDisabled(withdraw, state.busy);
            }
        }

        private void BuildDeleteConfirmation(VisualElement content, AppState state)
        {
            Text(content, "Delete your account?", 28, true).style.marginTop = 22f;
            Label explanation = Text(content, "This removes your account, the stickers you left, and your own collection. Other people's access to notes you wrote will be revoked at their next sync.", 16);
            explanation.style.marginTop = 12f;
            explanation.style.marginBottom = 15f;
            if (NftPresentation.RequiresAcknowledgement(state)) BuildNftTransferOut(content, state);
            Text(content, "Type DELETE to confirm", 15, true);
            PaperField confirmation = new PaperField("Confirmation", deleteConfirmation, 16, false,
                "Type DELETE to confirm account deletion");
            deleteField = confirmation;
            confirmation.name = "Delete confirmation";
            confirmation.tooltip = "Type DELETE to confirm account deletion";
            confirmation.style.fontSize = Mathf.RoundToInt(17f * textScale);
            confirmation.RegisterCallback<FocusInEvent>(_ => focusedField = confirmation);
            confirmation.RegisterCallback<FocusOutEvent>(_ => { if (focusedField == confirmation) focusedField = null; });
            content.Add(confirmation);
            Button delete = Action(content, "Permanently delete account", () =>
            {
                if (!NftPresentation.CanDelete(controller.State, deleteConfirmation)) return;
                controller.DeleteAccount();
                deleteConfirmation = "";
            });
            deleteButton = delete;
            delete.AddToClassList("danger");
            delete.style.marginTop = 13f;
            SetDisabled(delete, !NftPresentation.CanDelete(state, deleteConfirmation));
            confirmation.RegisterValueChangedCallback(evt =>
            {
                deleteConfirmation = evt.newValue;
                SetDisabled(delete, !NftPresentation.CanDelete(controller.State, deleteConfirmation));
            });
            Action(content, "Keep my account", () =>
            {
                accountScreen = AccountScreen.Overview;
                QueueRender();
            }, false).style.marginTop = 12f;
        }

        private void BuildNftTransferOut(VisualElement content, AppState state)
        {
            Text(content, "Keep your NFTs", 20, true);
            Text(content, "Send souvenirs to an Ethereum wallet you control before deleting your sign-in. Transfers use Sepolia test ETH from your souvenir wallet; minting is still covered by tagtag.", 14, false, Muted).style.marginTop = 8f;
            walletStatusLabel = Text(content, NftPresentation.WalletStatus(state), 13, false, Muted);
            var destination = new PaperField("Recipient wallet", transferRecipient, 42, false, "Ethereum address on Sepolia, beginning with 0x");
            destination.name = "NFT recipient";
            destination.RegisterValueChangedCallback(evt => { transferRecipient = evt.newValue; nftTransferContents.Reset(); RefreshNftTransferRows(controller.State); });
            content.Add(destination);
            Text(content, "Check the full address carefully. Transfers cannot be undone.", 13, false, Muted).style.marginTop = 6f;
            nftTransferHost = Column(content);
            nftTransferContents.Reset();
            RefreshNftTransferRows(state);
            Action(content, "Refresh transfer status", () => (controller as INftTransferController)?.RefreshNftTransfers(), false);
            string walletExplorer = NftPresentation.WalletExplorerUrl(state.walletAddress);
            if (!string.IsNullOrEmpty(walletExplorer)) Action(content, "View wallet on Sepolia", () => Application.OpenURL(walletExplorer), false);
            Text(content, "NFTs remain on-chain. Pending mints, incomplete transfers, or NFTs left in this wallet may become inaccessible after you delete your sign-in. You can keep your account and finish transfers later.", 14, false, Muted).style.marginTop = 12f;
            var acknowledge = new PaperSwitch("I understand and accept possible NFT access loss", state.nftDeletionAcknowledged);
            acknowledge.name = "Acknowledge NFT access loss";
            acknowledge.style.whiteSpace = WhiteSpace.Normal;
            acknowledge.style.minHeight = 44f;
            acknowledge.style.marginTop = 10f;
            acknowledge.style.marginBottom = 15f;
            acknowledge.Q(className: "switch-track").style.flexShrink = 0;
            acknowledge.RegisterValueChangedCallback(evt => (controller as INftTransferController)?.AcknowledgeNftLoss(evt.newValue));
            content.Add(acknowledge);
        }

        private void RefreshNftTransferRows(AppState state)
        {
            if (nftTransferHost == null) return;
            string key = state.busy + ":" + state.walletStatus + ":" + transferRecipient;
            foreach (var item in state.collection) key += ":" + item.id + ":" + item.nft?.status;
            foreach (var item in state.nftTransfers) key += ":" + item.stickerId + ":" + item.status + ":" + item.message;
            if (!nftTransferContents.NeedsRefresh(key)) return;
            nftTransferHost.Clear();
            bool found = false;
            foreach (var sticker in state.collection)
            {
                if (string.IsNullOrEmpty(sticker.nft?.status)) continue;
                found = true;
                Divider(nftTransferHost);
                Text(nftTransferHost, Safe(sticker.place, "Collected souvenir"), 16, true);
                var transfer = state.nftTransfers.Find(item => item.stickerId == sticker.id);
                Text(nftTransferHost, transfer == null ? NftPresentation.Status(sticker.nft) : transfer.message, 14, false, Muted);
                if (!string.IsNullOrEmpty(transfer?.transactionHash))
                {
                    string explorer = NftPresentation.ExplorerUrl(new NftStatus { chainId = 11155111, transactionHash = transfer.transactionHash });
                    if (!string.IsNullOrEmpty(explorer)) Action(nftTransferHost, "View transfer", () => Application.OpenURL(explorer), false);
                }
                if (sticker.nft.status == "confirmed" && (transfer == null || transfer.status == "failed"))
                {
                    string stickerId = sticker.id;
                    var send = Action(nftTransferHost, "Send this NFT", () => (controller as INftTransferController)?.TransferNft(stickerId, transferRecipient));
                    SetDisabled(send, state.busy || state.walletStatus != "ready" || !NftPresentation.ValidRecipient(transferRecipient, state.walletAddress));
                }
            }
            if (!found) Text(nftTransferHost, "No minted souvenirs in your current book.", 14, false, Muted);
        }

        private void OpenReport(string stickerId)
        {
            sheet = Sheet.Report;
            sheetStickerId = stickerId;
            selectedReportReason = ReportReasons[0];
            QueueRender();
        }

        private void OpenBlock(string authorId)
        {
            sheet = Sheet.Block;
            sheetAuthorId = authorId;
            QueueRender();
        }

        private PaperSheet sheetView;
        private Button sheetSubmitButton;
        private VisualElement sheetDetailHost;
        private readonly PresenterCache collectedDetailContents = new PresenterCache();
        private bool noteFieldsBusy;
        private PaperField noteFocusAfterBusy;
        private int noteFocusCursor;
        private int noteFocusSelection;
        private readonly List<PaperSelection> reportChoices = new List<PaperSelection>();
        private readonly List<Button> inventoryChoices = new List<Button>();
        private Button creationImportButton, creationCameraButton, creationAiButton;
        private Button creationRetryButton, creationMyDesignsButton;
        private Button deleteDesignConfirmButton, deleteDesignCancelButton;
        private bool deleteDesignPending;
        private Label creationImportNotice, creationCameraNotice, creationAiNotice;
        private Label creationSaveNotice, creationDesignError;

        private void BuildSheet(AppState state)
        {
            noteFieldsBusy = false;
            noteFocusAfterBusy = null;
            VisualElement scrim = new VisualElement();
            scrim.name = "Sheet scrim";
            scrim.AddToClassList("sheet-scrim");
            scrim.RegisterCallback<PointerDownEvent>(_ => RequestCloseSheet());
            overlayHost.Add(scrim);
            string title = sheet == Sheet.Privacy ? "Privacy Policy" : sheet == Sheet.Terms ? "Terms & Conditions" : sheet == Sheet.ReferencePhoto ? "Original spot" : sheet == Sheet.Picker ? "My Stickers" : sheet == Sheet.Creator ? "Add Sticker" :
                sheet == Sheet.HomeDesignPreview ? "Your design" : sheet == Sheet.DeleteDesign ? "Remove design" : sheet == Sheet.Note ? "Your Note" :
                sheet == Sheet.Collected ? "Collected sticker" : sheet == Sheet.Report ? "Report sticker" :
                sheet == Sheet.Withdraw ? "Withdraw sticker" : "Block author";
            Sheet openedSheet = sheet;
            string collectedId = sheetStickerId;
            string designId = sheetDesignId;
            Action returnFocus = IsLegalSheet ? () => FocusSheetTrigger(openedSheet) : openedSheet == Sheet.ReferencePhoto ? () => FocusSheetTrigger(openedSheet) :
                openedSheet == Sheet.HomeDesignPreview ? () => FocusHomeDesign(designId) :
                openedSheet == Sheet.Note || openedSheet == Sheet.Picker || openedSheet == Sheet.Creator ?
                () => FocusSheetTrigger(openedSheet) : openedSheet == Sheet.Collected ?
                () => FocusCollectedCell(collectedId) : null;
            Func<bool> dismissalGuard = sheet == Sheet.HomeDesignPreview || sheet == Sheet.DeleteDesign ?
                CanDismissSheet : null;
            sheetView = new PaperSheet(title, CloseSheet, reducedMotion, returnFocus, "Close", dismissalGuard);
            if (sheet == Sheet.Picker || sheet == Sheet.Creator || IsLegalSheet)
            {
                sheetView.style.flexDirection = FlexDirection.Column;
                sheetView.Grip.style.flexShrink = 0f;
                sheetView.Q(className: "sheet-heading").style.flexShrink = 0f;
                sheetView.Scroll.style.flexGrow = 1f;
                sheetView.Scroll.style.flexShrink = 1f;
                sheetView.Scroll.style.minHeight = 0f;
            }
            sheetView.Q<Label>(className: "sheet-title").style.unityFont = HeadingFont;
            sheetView.style.bottom = SheetBottom();
            ApplySheetHeight();
            overlayHost.Add(sheetView);
            sheetView.RegisterCallback<GeometryChangedEvent>(_ => ApplySheetHeight());
            VisualElement content = sheetView.Scroll.contentContainer;
            content.style.paddingBottom = 18f;
            reportChoices.Clear();
            sheetSubmitButton = null;
            publishButton = null;
            sheetDetailHost = null;
            inventoryChoices.Clear();
            creationImportButton = creationCameraButton = creationAiButton = null;
            creationRetryButton = creationMyDesignsButton = null;
            deleteDesignConfirmButton = deleteDesignCancelButton = null;
            homePreviewPlaceButton = homePreviewRemoveButton = null;
            homeDesignActionError = null;
            creationImportNotice = creationCameraNotice = creationAiNotice = null;
            creationSaveNotice = creationDesignError = null;
            if (IsLegalSheet) BuildLegalContent(content);
            else if (sheet == Sheet.ReferencePhoto) BuildReferencePhotoSheet(content);
            else if (sheet == Sheet.Picker) BuildPickerSheet(content, state);
            else if (sheet == Sheet.Creator) BuildCreatorSheet(content, state);
            else if (sheet == Sheet.HomeDesignPreview) BuildHomeDesignPreview(content, state);
            else if (sheet == Sheet.DeleteDesign) BuildDeleteDesignSheet(content, state);
            else if (sheet == Sheet.Note) BuildNoteSheet(content, state);
            else if (sheet == Sheet.Collected)
            {
                sheetDetailHost = Column(content);
                collectedDetailContents.Reset();
            }
            else if (sheet == Sheet.Report) BuildReportSheet(content, state);
            else if (sheet == Sheet.Withdraw) BuildWithdrawSheet(content, state);
            else BuildBlockSheet(content, state);
            if (sheet != Sheet.Creator && sheet != Sheet.ReferencePhoto && !IsLegalSheet) AddStatus(content, state);
            sheetView.BindGestures();
            RefreshSheet(state);
            if (!string.IsNullOrEmpty(returnFieldName) && sheet == Sheet.Note)
            {
                string fieldName = returnFieldName;
                int cursor = returnCursor, selection = returnSelection;
                returnFieldName = null;
                sheetView.schedule.Execute(() =>
                {
                    PaperField field = sheetView?.Q<PaperField>(fieldName);
                    if (field == null || field.panel == null) return;
                    field.Focus();
                    field.SelectRange(Mathf.Clamp(cursor, 0, field.value.Length),
                        Mathf.Clamp(selection, 0, field.value.Length));
                });
            }
        }

        private void FocusSheetTrigger(Sheet closed)
        {
            string name = closed == Sheet.Privacy ? "Login Privacy Policy" : closed == Sheet.Terms ? "Login Terms & Conditions" : closed == Sheet.ReferencePhoto ? "Original spot preview" : closed == Sheet.Note ? "STICK Inventory" :
                closed == Sheet.Picker || closed == Sheet.Creator ?
                    controller.State.page == AppPage.Home ? "Home Make sticker" : "STICK Inventory" : null;
            if (name == null) return;
            screenHost?.schedule.Execute(() => screenHost.Q<Button>(name)?.Focus());
        }

        private void RequestCloseSheet()
        {
            if (!CanDismissSheet()) return;
            if (sheetView != null) sheetView.Dismiss();
            else CloseSheet();
        }

        private bool CanDismissSheet()
        {
            return !(sheet == Sheet.DeleteDesign && deleteDesignPending) &&
                !(sheet == Sheet.HomeDesignPreview && homePlacementPending);
        }

        private void BuildPickerSheet(VisualElement content, AppState state)
        {
            VisualElement grid = new VisualElement { name = "Sticker inventory grid" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.FlexStart;
            content.Add(grid);
            foreach (StickerDesign design in state.designs)
            {
                if (design == null || string.IsNullOrEmpty(design.id)) continue;
                string designId = design.id;
                bool canUse = SignedIn(state) ? design.ownerId == state.user.uid : string.IsNullOrEmpty(design.ownerId);
                PaperSelection choice = new PaperSelection("", state.selectedDesign == designId, () => controller.SelectDesign(designId));
                choice.name = "Inventory Design " + designId;
                choice.tooltip = "Select " + Safe(design.name, "sticker");
                StyleInventoryTile(choice);
                Image art = Art(choice, design, 76f);
                art.pickingMode = PickingMode.Ignore;
                choice.userData = canUse;
                SetDisabled(choice, state.busy || !canUse);
                grid.Add(choice);
                inventoryChoices.Add(choice);
            }
            for (int index = 0; index < Presets.Length; index++)
            {
                string preset = Presets[index];
                string presetName = PaperFlow.PresetName(preset);
                PaperSelection choice = new PaperSelection("", state.selectedPreset == preset, () => controller.SelectPreset(preset));
                choice.name = "Inventory " + presetName;
                choice.tooltip = "Select " + presetName;
                StyleInventoryTile(choice);
                Image art = Art(choice, preset, 76f);
                art.pickingMode = PickingMode.Ignore;
                choice.userData = true;
                SetDisabled(choice, state.busy);
                grid.Add(choice);
                inventoryChoices.Add(choice);
            }
            Button add = Action(grid, "Add Sticker", () =>
            {
                sheet = Sheet.Creator;
                QueueRender();
            }, false);
            add.name = "Add Sticker";
            add.tooltip = "Create a sticker";
            StyleInventoryTile(add);
            add.style.unityTextAlign = TextAnchor.MiddleCenter;
            add.style.whiteSpace = WhiteSpace.Normal;
        }

        private void RefreshPicker(AppState state)
        {
            foreach (Button choice in inventoryChoices)
                SetDisabled(choice, state.busy || !(choice.userData is bool canUse && canUse));
        }

        private void StyleInventoryTile(VisualElement tile)
        {
            if (tile is PaperSelection selection) selection.ShowDottedOutline = false;
            tile.style.width = Length.Percent(31.5f);
            tile.style.flexBasis = Length.Percent(31.5f);
            tile.style.flexGrow = 0f;
            tile.style.flexShrink = 0f;
            tile.style.minHeight = textScale > 1.2f ? 112f : 104f;
            tile.style.marginRight = Length.Percent(1.5f);
            tile.style.marginBottom = 8f;
            tile.style.paddingLeft = 4f;
            tile.style.paddingRight = 4f;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
        }

        private void BuildCreatorSheet(VisualElement content, AppState state)
        {
            Button back = Action(content, "Back to stickers", () =>
            {
                sheet = Sheet.Picker;
                QueueRender();
            }, false);
            back.name = "Back to stickers";
            back.style.alignSelf = Align.FlexStart;
            back.style.marginBottom = 14f;
            Text(content, "Create a sticker", 21, true);
            VisualElement sources = Row(content);
            sources.name = "Creator sources";
            sources.style.justifyContent = Justify.SpaceBetween;
            sources.style.marginTop = 12f;
            creationImportButton = CreatorTile(sources, "Upload", "upload", "import");
            creationCameraButton = CreatorTile(sources, "Photo", "photo", "polaroid");
            creationAiButton = CreatorTile(sources, "Imagine", "imagine", "ai");
            creationImportNotice = Text(content, "", 13, false, Muted);
            creationCameraNotice = Text(content, "", 13, false, Muted);
            creationAiNotice = Text(content, "", 13, false, Muted);
            creationSaveNotice = Text(content, "", 14, false, Muted);
            creationSaveNotice.name = "Creator save notice";
            creationSaveNotice.style.marginTop = 12f;
            creationDesignError = Text(content, "", 14, false, new Color32(125, 39, 31, 255));
            creationDesignError.name = "Creator design error";
            creationDesignError.style.marginTop = 8f;
            creationRetryButton = Action(content, "Retry saving sticker", () =>
            {
                if (SignedIn(controller.State)) controller.RetryDesignSave();
                else OpenSignIn();
            }, false);
            PaperDottedOutline.Decorate(creationRetryButton, capsule: true);
            creationRetryButton.style.alignSelf = Align.FlexStart;
            creationRetryButton.style.marginTop = 6f;
            creationMyDesignsButton = Action(content, "My designs", OpenHomeDesignsFromCreator, false);
            creationMyDesignsButton.name = "Creator My designs";
            creationMyDesignsButton.style.alignSelf = Align.FlexStart;
            creationMyDesignsButton.style.marginTop = 14f;
            RefreshCreation(state);
        }

        private Button CreatorTile(VisualElement parent, string label, string artwork, string source)
        {
            Button tile = Action(parent, "", () => controller.CreateSticker(source), false);
            tile.name = "Creator " + label;
            tile.tooltip = label + " a sticker";
            tile.RemoveFromClassList("quiet");
            tile.AddToClassList("secondary");
            PaperDottedOutline.Decorate(tile);
            tile.style.width = Length.Percent(31.5f);
            tile.style.minWidth = 0f;
            tile.style.minHeight = textScale > 1.2f ? 142f : 124f;
            tile.style.flexDirection = FlexDirection.Column;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.paddingLeft = 4f;
            tile.style.paddingRight = 4f;
            Image image = new Image
            {
                image = Resources.Load<Texture2D>("Tagtag/Creation/" + artwork),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                name = "Creator " + label + " art"
            };
            image.style.width = 74f;
            image.style.height = 74f;
            image.style.maxWidth = Length.Percent(100f);
            tile.Add(image);
            Label title = Text(tile, label, 14, true);
            title.pickingMode = PickingMode.Ignore;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginTop = 5f;
            return tile;
        }

        private void RefreshCreation(AppState state)
        {
            if (creationImportButton == null) return;
            int available = state.creationCapabilities;
            SetDisabled(creationImportButton, !PaperCreation.CanStart("import", state));
            SetDisabled(creationCameraButton, !PaperCreation.CanStart("polaroid", state));
            SetDisabled(creationAiButton, !PaperCreation.CanStart("ai", state));
            creationImportNotice.text = (available & 1) == 0 ? "Photo and file import is unavailable on this device." : "";
            creationCameraNotice.text = (available & (1 | 2)) == 0 ? "Photo creation is unavailable on this device." : "";
            creationAiNotice.text = (available & 4) == 0 ? "Image Playground is unavailable on this device." : "";
            foreach (Label notice in new[] { creationImportNotice, creationCameraNotice, creationAiNotice })
                notice.style.display = string.IsNullOrEmpty(notice.text) ? DisplayStyle.None : DisplayStyle.Flex;
            bool saved = !state.busy && !state.hasPendingDesign && string.IsNullOrEmpty(state.designError) &&
                state.status != null && state.status.StartsWith("Saved to My Stickers.", StringComparison.Ordinal);
            creationSaveNotice.text = state.busy && state.hasPendingDesign ? "Saving your sticker…" :
                state.hasPendingDesign && !SignedIn(state) ?
                    "Your sticker is on this device. Sign in to save it before making another." :
                state.hasPendingDesign ? "Your sticker is on this device. Retry saving it before making another." :
                saved ? "Saved to My designs." : "";
            creationSaveNotice.style.display = string.IsNullOrEmpty(creationSaveNotice.text) ? DisplayStyle.None : DisplayStyle.Flex;
            creationDesignError.text = state.designError ?? "";
            creationDesignError.style.display = string.IsNullOrEmpty(creationDesignError.text) ? DisplayStyle.None : DisplayStyle.Flex;
            creationRetryButton.style.display = state.hasPendingDesign ? DisplayStyle.Flex : DisplayStyle.None;
            creationRetryButton.text = SignedIn(state) ? "Retry saving sticker" : "Sign in to save";
            SetDisabled(creationRetryButton, state.busy);
            SetDisabled(creationMyDesignsButton, state.busy);
        }

        private void BuildDeleteDesignSheet(VisualElement content, AppState state)
        {
            StickerDesign design = state.designs.Find(item => item != null && item.id == sheetDesignId);
            Text(content, "Remove " + Safe(design?.name, "this design") + "?", 21, true);
            Text(content, "It will leave My Stickers. Stickers already placed or collected keep their artwork.",
                15, false, Muted).style.marginTop = 8f;
            deleteDesignConfirmButton = Action(content, "Remove design", () =>
            {
                if (deleteDesignPending) return;
                deleteDesignPending = true;
                controller.DeleteDesign(sheetDesignId);
                QueueRender();
            });
            deleteDesignConfirmButton.AddToClassList("danger");
            deleteDesignConfirmButton.style.marginTop = 18f;
            SetDisabled(deleteDesignConfirmButton, state.busy || design == null || deleteDesignPending);
            deleteDesignCancelButton = Action(content, "Cancel", () =>
            {
                if (deleteDesignPending) return;
                if (deleteDesignFromHome) sheet = Sheet.HomeDesignPreview;
                else { sheetDesignId = null; sheet = Sheet.Creator; }
                QueueRender();
            }, false);
            deleteDesignCancelButton.style.marginTop = 8f;
            SetDisabled(deleteDesignCancelButton, deleteDesignPending);
            BuildDesignActionError(content);
            RefreshDesignActionError(state);
        }

        private void SyncDeleteDesign(AppState state)
        {
            if (!deleteDesignPending) return;
            if (sheet != Sheet.DeleteDesign) { deleteDesignPending = false; return; }
            if (!state.designs.Exists(item => item != null && item.id == sheetDesignId))
            {
                deleteDesignPending = false;
                sheetDesignId = null;
                sheet = deleteDesignFromHome ? Sheet.None : Sheet.Creator;
                deleteDesignFromHome = false;
            }
            else if (!state.busy && !string.IsNullOrEmpty(state.designError)) deleteDesignPending = false;
        }

        private void BuildNoteSheet(VisualElement content, AppState state)
        {
            DraftField(content, "Title", draftPlace, 80, false, value => draftPlace = value,
                "Give your sticker a name people can see on the map.");
            DraftField(content, "Your note", draftNote, 2000, true, value => draftNote = value,
                "Unlocked only when someone taps your sticker in AR.");
            if (!SignedIn(state))
            {
                Text(content, "Sign in to publish. Your draft will stay here.", 14, false, Muted).style.marginTop = 12f;
                sheetSubmitButton = Action(content, "Sign in to publish", OpenSignIn);
                PaperDottedOutline.Decorate(sheetSubmitButton, capsule: true);
            }
            else
            {
                publishButton = Action(content, "Publish sticker", () =>
                {
                    if (!PaperFlow.CanPresentPublish(draftPlace, draftTeaser, draftNote,
                        controller.State.hasCapturedSpot, controller.State.busy, controller.State.hasPendingPublication)) return;
                    publicationRequested = true;
                    controller.SetDraft(draftPlace, draftTeaser, draftNote);
                    controller.Publish();
                });
                PaperDottedOutline.Decorate(publishButton, capsule: true);
                publishButton.style.marginTop = 14f;
                RefreshPublish(state);
            }
        }

        private void RefreshSheet(AppState state)
        {
            if (sheetView == null || sheet == Sheet.None) return;
            sheetView.style.bottom = SheetBottom();
            ApplySheetHeight();
            if (sheet == Sheet.Note)
            {
                RefreshNoteFieldAvailability(state);
                RefreshPublish(state);
            }
            if (sheet == Sheet.Collected) RefreshCollectedDetail(state);
            if (sheet == Sheet.Picker) RefreshPicker(state);
            if (sheet == Sheet.Creator) RefreshCreation(state);
            if (sheet == Sheet.HomeDesignPreview) RefreshHomeDesignPreview(state);
            if (sheet == Sheet.DeleteDesign)
            {
                if (deleteDesignConfirmButton != null) SetDisabled(deleteDesignConfirmButton,
                    state.busy || deleteDesignPending);
                if (deleteDesignCancelButton != null) SetDisabled(deleteDesignCancelButton, deleteDesignPending);
                RefreshDesignActionError(state);
            }
            Button close = sheetView.Q<Button>("Action Close");
            if (close != null) SetDisabled(close, !CanDismissSheet());
            if (sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy);
            foreach (PaperSelection choice in reportChoices)
                choice.SetSelected(choice.userData is string reason && reason == selectedReportReason);
            if (sheet == Sheet.Report && sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy || !SignedIn(state));
            if (sheet == Sheet.Block && sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy || !SignedIn(state));
            if (sheet == Sheet.Withdraw && sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy);
            UpdateStatus(state);
        }

        private void RefreshNoteFieldAvailability(AppState state)
        {
            if (state.busy && !noteFieldsBusy)
            {
                VisualElement focused = sheetView.panel?.focusController?.focusedElement as VisualElement;
                PaperField field = focused as PaperField ?? focused?.GetFirstAncestorOfType<PaperField>();
                noteFocusAfterBusy = field != null && sheetView.Contains(field) ? field : null;
                if (noteFocusAfterBusy != null)
                {
                    noteFocusCursor = noteFocusAfterBusy.cursorIndex;
                    noteFocusSelection = noteFocusAfterBusy.selectIndex;
                }
            }

            foreach (string name in new[] { "Title", "Your note" })
            {
                PaperField field = sheetView.Q<PaperField>(name);
                if (field != null && field.enabledSelf != !state.busy) field.SetEnabled(!state.busy);
            }

            if (!state.busy && noteFieldsBusy && noteFocusAfterBusy != null)
            {
                PaperField restore = noteFocusAfterBusy;
                int cursor = noteFocusCursor, selection = noteFocusSelection;
                restore.schedule.Execute(() =>
                {
                    if (sheet != Sheet.Note || controller.State.busy || sheetView == null ||
                        restore.panel == null || !restore.enabledInHierarchy || !sheetView.Contains(restore)) return;
                    restore.Focus();
                    restore.SelectRange(Mathf.Clamp(cursor, 0, restore.value.Length),
                        Mathf.Clamp(selection, 0, restore.value.Length));
                });
                noteFocusAfterBusy = null;
            }
            noteFieldsBusy = state.busy;
        }

        private void RefreshCollectedDetail(AppState state)
        {
            if (sheetDetailHost == null) return;
            CollectedSticker detail = state.detail != null && state.detail.id == sheetStickerId ? state.detail : null;
            if (!collectedDetailContents.NeedsRefresh(CollectionPresentation.DetailKey(detail, state.busy))) return;
            var continuity = PaperNavigationMotion.RebuildState.Capture(sheetDetailHost);
            sheetDetailHost.Clear();
            BuildCollectedDetail(sheetDetailHost, state);
            continuity.Restore(sheetDetailHost, () => sheet == Sheet.Collected);
        }

        private float SheetBottom()
        {
            float pixels = lastKeyboardHeight > 0f ? Mathf.Max(Screen.safeArea.yMin, lastKeyboardHeight) : 0f;
            return pixels * (root != null && root.layout.height > 0f && Screen.height > 0 ?
                root.layout.height / Screen.height : 1f);
        }

        private void ApplySheetHeight()
        {
            if (sheetView == null || root == null) return;
            float scale = root.layout.height > 0f && Screen.height > 0 ? root.layout.height / Screen.height : 1f;
            float safeBottom = lastKeyboardHeight > 0f ? 0f : Mathf.Max(0f, Screen.safeArea.yMin) * scale;
            sheetView.style.paddingBottom = 18f + safeBottom;
            if (float.IsNaN(root.layout.height) || float.IsInfinity(root.layout.height) || root.layout.height <= 0f || Screen.height <= 0)
            {
                sheetView.style.maxHeight = Length.Percent(82f);
                if (sheet == Sheet.Picker || sheet == Sheet.Creator)
                {
                    sheetView.style.top = Length.Percent(18f);
                    sheetView.style.bottom = StyleKeyword.Auto;
                    sheetView.style.height = Length.Percent(82f);
                }
                return;
            }
            float topInset = (Screen.height - Screen.safeArea.yMax) * scale;
            float available = Mathf.Max(180f, root.layout.height - SheetBottom() - topInset - 8f);
            if (sheet != Sheet.Picker && sheet != Sheet.Creator)
            {
                sheetView.style.maxHeight = available;
                return;
            }
            float maxHeight = Mathf.Min(root.layout.height * .82f, available);
            sheetView.style.top = root.layout.height - SheetBottom() - maxHeight;
            sheetView.style.bottom = StyleKeyword.Auto;
            sheetView.style.maxHeight = maxHeight;
            sheetView.style.height = maxHeight;
        }

        private void BuildCollectedDetail(VisualElement content, AppState state)
        {
            CollectedSticker sticker = state.detail;
            if (sticker == null || sticker.id != sheetStickerId)
            {
                Text(content, state.busy ? "Opening your sticker…" : "This sticker is unavailable in your collection.", 16, false, Muted);
                return;
            }
            Image artwork = Art(content, sticker, 140f, false);
            AddArtworkNotice(content, artwork, sticker.designId, false);
            Text(content, Safe(sticker.place, "A place you visited"), 25, true).style.marginTop = 8f;
            Text(content, "Left by " + Safe(sticker.authorName, "someone nearby"), 14, false, Muted).style.marginTop = 4f;
            Text(content, "Collected " + Date(sticker.collectedAt), 13, false, Muted).style.marginTop = 2f;
            if (!string.IsNullOrEmpty(NftPresentation.Status(sticker.nft)))
            {
                Text(content, NftPresentation.Status(sticker.nft), 14, false, Muted).style.marginTop = 10f;
                string explorer = NftPresentation.ExplorerUrl(sticker.nft);
                if (!string.IsNullOrEmpty(explorer))
                    Action(content, "View NFT transaction", () => Application.OpenURL(explorer), false);
            }
            Divider(content);
            Text(content, "The clue", 14, true);
            Text(content, Safe(sticker.teaser, "No clue available"), 16).style.marginTop = 5f;
            Divider(content);
            Text(content, "The note", 14, true);
            Text(content, sticker.unavailable ? "This note is no longer available." : Safe(sticker.note, "No note available."), 17).style.marginTop = 7f;
            VisualElement actions = Row(content);
            actions.style.marginTop = 20f;
            SetDisabled(Action(actions, "Report", null, false), true);
            if (!string.IsNullOrEmpty(sticker.authorId)) SetDisabled(Action(actions, "Block author", null, false), true);
        }

        private void BuildReportSheet(VisualElement content, AppState state)
        {
            Text(content, "Tell us what is wrong", 18, true);
            Text(content, "Your report goes to moderation. The sticker remains hidden from no one until it is reviewed.", 14, false, Muted).style.marginTop = 6f;
            foreach (string reason in ReportReasons)
            {
                string choice = reason;
                PaperSelection button = new PaperSelection(reason, selectedReportReason == reason, () =>
                {
                    selectedReportReason = choice;
                    RefreshSheet(controller.State);
                });
                button.userData = reason;
                content.Add(button);
                reportChoices.Add(button);
                button.style.marginTop = 8f;
            }
            Button submit = Action(content, "Send report", () =>
            {
                controller.Report(sheetStickerId, selectedReportReason);
                RequestCloseSheet();
            });
            sheetSubmitButton = submit;
            submit.style.marginTop = 16f;
            SetDisabled(submit, state.busy || !SignedIn(state));
            if (!SignedIn(state)) Action(content, "Sign in to report", OpenSignIn, false).style.marginTop = 7f;
        }

        private void BuildBlockSheet(VisualElement content, AppState state)
        {
            Text(content, "Hide this author's stickers?", 19, true);
            Text(content, "Their stickers will stop appearing in your nearby results after you block them.", 15, false, Muted).style.marginTop = 8f;
            Button block = Action(content, "Block author", () =>
            {
                controller.Block(sheetAuthorId);
                RequestCloseSheet();
            });
            sheetSubmitButton = block;
            block.style.marginTop = 18f;
            SetDisabled(block, state.busy || !SignedIn(state));
            if (!SignedIn(state)) Action(content, "Sign in to block", OpenSignIn, false).style.marginTop = 7f;
        }

        private void BuildWithdrawSheet(VisualElement content, AppState state)
        {
            Text(content, "Stop new discoveries?", 19, true);
            Text(content, "This sticker will disappear from Explore and AR discovery. Copies already collected remain in other books.", 15, false, Muted).style.marginTop = 8f;
            Button withdraw = Action(content, "Withdraw sticker", () =>
            {
                controller.Withdraw(sheetStickerId);
                RequestCloseSheet();
            });
            sheetSubmitButton = withdraw;
            withdraw.style.marginTop = 18f;
            SetDisabled(withdraw, state.busy);
        }

        private void CloseSheet()
        {
            if (!CanDismissSheet()) return;
            if (sheet == Sheet.DeleteDesign && !deleteDesignPending && deleteDesignFromHome)
            {
                sheet = Sheet.HomeDesignPreview;
                QueueRender();
                return;
            }
            if (sheet == Sheet.DeleteDesign && !deleteDesignPending && controller.State.creationOpen)
            {
                sheetDesignId = null;
                sheet = Sheet.Creator;
                QueueRender();
                return;
            }
            if (sheet == Sheet.Picker || sheet == Sheet.Creator) controller.CloseCreation();
            if (sheet == Sheet.Collected && controller.State.detail != null) controller.CloseDetail();
            sheet = Sheet.None;
            sheetStickerId = null;
            sheetDesignId = null;
            deleteDesignPending = false;
            deleteDesignFromHome = false;
            sheetAuthorId = null;
            sheetView = null;
            publishButton = null;
            QueueRender();
        }
    }
}
