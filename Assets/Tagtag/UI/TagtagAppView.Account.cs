using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private Label accountCollectionCount;
        private Label accountAuthoredCount;
        private VisualElement authoredListHost;
        private readonly PresenterCache authoredListContents = new PresenterCache();
        private PaperSwitch accountMotionSwitch;
        private readonly List<PaperSelection> textSizeChoices = new List<PaperSelection>();
        private readonly List<Button> authoredWithdrawButtons = new List<Button>();
        private Button appleSignInButton;
        private Button googleSignInButton;
        private Button deleteButton;
        private PaperField deleteField;

        private void BuildAccount(AppState state)
        {
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
                    accountScreen = AccountScreen.Overview;
                    QueueRender();
                }
            }, false);
            Text(heading, accountScreen == AccountScreen.SignIn ? "Sign in" : accountScreen == AccountScreen.Authored ? "Your stickers" : accountScreen == AccountScreen.DeleteConfirmation ? "Delete account" : "Account", 21, true);

            ScrollView scroll = PaperScroll(screenHost);
            scroll.name = "Account scroll";
            scroll.style.paddingLeft = 24f;
            scroll.style.paddingRight = 24f;
            VisualElement content = scroll.contentContainer;

            if (accountScreen == AccountScreen.SignIn && !SignedIn(state)) BuildSignIn(content, state);
            else if (accountScreen == AccountScreen.Authored) BuildAuthored(content, state);
            else if (accountScreen == AccountScreen.DeleteConfirmation) BuildDeleteConfirmation(content, state);
            else BuildAccountOverview(content, state);
            AddStatus(content, state);
            RefreshAccount(state);
        }

        private void RefreshAccount(AppState state)
        {
            if (accountCollectionCount != null) accountCollectionCount.text = CollectionPresentation.OrderedDistinct(state.collection).Count.ToString();
            if (accountAuthoredCount != null) accountAuthoredCount.text = state.authored.Count.ToString();
            if (accountMotionSwitch != null && accountMotionSwitch.value != reducedMotion) accountMotionSwitch.SetValueWithoutNotify(reducedMotion);
            foreach (PaperSelection size in textSizeChoices)
                if (size.userData is float value) size.SetSelected(Mathf.Abs(textScale - value) < .01f);
            if (deleteButton != null) SetDisabled(deleteButton, deleteConfirmation != "DELETE" || state.busy);
            if (appleSignInButton != null) SetDisabled(appleSignInButton, state.busy || !state.servicesConfigured);
            if (googleSignInButton != null) SetDisabled(googleSignInButton, state.busy || !state.servicesConfigured);
            foreach (Button button in authoredWithdrawButtons) SetDisabled(button, state.busy);
            if (authoredListHost != null) RefreshAuthored(state);
        }

        private void BuildSignIn(VisualElement content, AppState state)
        {
            Text(content, "Keep the stickers you find", 29, true).style.marginTop = 26f;
            Label explanation = Text(content, "Explore freely. Sign in to leave a sticker or add one to your book. Your collection follows this account.", 16, false, Muted);
            explanation.style.marginTop = 12f;
            explanation.style.marginBottom = 24f;
            Button apple = Action(content, "Continue with Apple", () => controller.SignIn("apple"));
            appleSignInButton = apple;
            apple.AddToClassList("auth-provider-apple");
            apple.style.marginBottom = 10f;
            SetDisabled(apple, state.busy || !state.servicesConfigured);
            Button google = Action(content, "Continue with Google", () => controller.SignIn("google"));
            googleSignInButton = google;
            google.AddToClassList("auth-provider-google");
            SetDisabled(google, state.busy || !state.servicesConfigured);
            if (!state.servicesConfigured)
            {
                Text(content, "Sign-in is unavailable until the service is configured.", 14, false, Muted).style.marginTop = 12f;
            }
            Action(content, "Continue exploring", () =>
            {
                AbandonSignInReturn();
                controller.SetAccountOpen(false);
            }, false).style.marginTop = 20f;
        }

        private void BuildAccountOverview(VisualElement content, AppState state)
        {
            if (!SignedIn(state))
            {
                BuildSignIn(content, state);
                return;
            }

            Text(content, Safe(state.user.displayName, "Your account"), 28, true).style.marginTop = 22f;
            Text(content, "Your stickers and collection", 15, false, Muted).style.marginTop = 4f;
            Divider(content);
            VisualElement collection = Row(content);
            collection.style.justifyContent = Justify.SpaceBetween;
            collection.style.alignItems = Align.Center;
            Text(collection, "Collected stickers", 17, true);
            accountCollectionCount = Text(collection, CollectionPresentation.OrderedDistinct(state.collection).Count.ToString(), 17, false, Muted);
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
            Text(content, "Reading and motion", 20, true);
            Label size = Text(content, "Text size", 15, false, Muted);
            size.style.marginTop = 12f;
            VisualElement sizes = Row(content);
            sizes.style.marginTop = 7f;
            textSizeChoices.Clear();
            AddTextSize(sizes, "Standard", 1f);
            AddTextSize(sizes, "Larger", 1.2f);
            AddTextSize(sizes, "Largest", 1.4f);
            PaperSwitch motion = new PaperSwitch("Reduced motion", reducedMotion,
                "Page changes stay still. AR movement follows your camera.");
            accountMotionSwitch = motion;
            content.Add(motion);
            motion.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != motion) return;
                reducedMotion = evt.newValue;
                PlayerPrefs.SetInt("tagtag.reducedMotion", reducedMotion ? 1 : 0);
                PlayerPrefs.Save();
                root.EnableInClassList("reduced-motion", reducedMotion);
            });
            motion.style.marginTop = 12f;
            Divider(content);
            Action(content, "Sign out", () =>
            {
                controller.SignOut();
                accountScreen = AccountScreen.SignIn;
            }, false).style.alignSelf = Align.FlexStart;
            Action(content, "Delete account and stickers", () =>
            {
                deleteConfirmation = "";
                accountScreen = AccountScreen.DeleteConfirmation;
                QueueRender();
            }, false).style.marginTop = 9f;
        }

        private void AddTextSize(VisualElement parent, string label, float scale)
        {
            PaperSelection button = new PaperSelection(label, Mathf.Abs(textScale - scale) < .01f, () =>
            {
                textScale = scale;
                PlayerPrefs.SetFloat("tagtag.textScale", scale);
                PlayerPrefs.Save();
                ApplyTextScale();
                RefreshAccount(controller.State);
            });
            button.userData = scale;
            button.style.unityFont = SemiboldFont;
            parent.Add(button);
            textSizeChoices.Add(button);
            button.style.flexGrow = 1f;
            button.style.marginRight = 4f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
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
            string key = state.authored.Count.ToString();
            foreach (StickerSummary item in state.authored)
                if (item != null) key += ":" + item.id + ":" + item.revision;
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
                Art(row, sticker.presetId, 62f);
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
                if (deleteConfirmation != "DELETE") return;
                controller.DeleteAccount();
                deleteConfirmation = "";
            });
            deleteButton = delete;
            delete.AddToClassList("danger");
            delete.style.marginTop = 13f;
            SetDisabled(delete, deleteConfirmation != "DELETE" || state.busy);
            confirmation.RegisterValueChangedCallback(evt =>
            {
                deleteConfirmation = evt.newValue;
                SetDisabled(delete, deleteConfirmation != "DELETE" || controller.State.busy);
            });
            Action(content, "Keep my account", () =>
            {
                accountScreen = AccountScreen.Overview;
                QueueRender();
            }, false).style.marginTop = 12f;
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
        private readonly List<PaperSelection> reportChoices = new List<PaperSelection>();

        private void BuildSheet(AppState state)
        {
            VisualElement scrim = new VisualElement();
            scrim.name = "Sheet scrim";
            scrim.AddToClassList("sheet-scrim");
            scrim.RegisterCallback<PointerDownEvent>(_ => RequestCloseSheet());
            overlayHost.Add(scrim);
            string title = sheet == Sheet.Picker ? "Choose Taggi" : sheet == Sheet.Note ? "Write note" :
                sheet == Sheet.Collected ? "Collected sticker" : sheet == Sheet.Report ? "Report sticker" :
                sheet == Sheet.Withdraw ? "Withdraw sticker" : "Block author";
            Sheet openedSheet = sheet;
            Action returnFocus = openedSheet == Sheet.Note || openedSheet == Sheet.Picker ?
                () => FocusSheetTrigger(openedSheet) : null;
            sheetView = new PaperSheet(title, CloseSheet, reducedMotion, returnFocus, "Close");
            sheetView.style.bottom = SheetBottom();
            ApplySheetHeight();
            overlayHost.Add(sheetView);
            VisualElement content = sheetView.Scroll.contentContainer;
            content.style.paddingBottom = 18f;
            reportChoices.Clear();
            sheetSubmitButton = null;
            publishButton = null;
            sheetDetailHost = null;
            if (sheet == Sheet.Picker) BuildPickerSheet(content, state);
            else if (sheet == Sheet.Note) BuildNoteSheet(content, state);
            else if (sheet == Sheet.Collected)
            {
                sheetDetailHost = Column(content);
                collectedDetailContents.Reset();
            }
            else if (sheet == Sheet.Report) BuildReportSheet(content, state);
            else if (sheet == Sheet.Withdraw) BuildWithdrawSheet(content, state);
            else BuildBlockSheet(content, state);
            AddStatus(content, state);
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
            string name = closed == Sheet.Note ? "Action Write note" : closed == Sheet.Picker ?
                (screenHost?.Q<Button>("Action Change pose") != null ? "Action Change pose" :
                    screenHost?.Q<Button>("Action Leave a sticker") != null ? "Action Leave a sticker" : "Action Write note") : null;
            if (name == null) return;
            screenHost?.schedule.Execute(() => screenHost.Q<Button>(name)?.Focus());
        }

        private void RequestCloseSheet()
        {
            if (sheetView != null) sheetView.Dismiss();
            else CloseSheet();
        }

        private void BuildPickerSheet(VisualElement content, AppState state)
        {
            Text(content, "Pick a pose for this place.", 16, false, Muted).style.marginBottom = 8f;
            for (int index = 0; index < Presets.Length; index++)
            {
                string preset = Presets[index];
                VisualElement row = Row(content);
                row.style.alignItems = Align.Center;
                Art(row, preset, 60f);
                PaperSelection choice = new PaperSelection("Taggi pose " + (index + 1),
                    state.selectedPreset == preset, () =>
                    {
                        controller.SelectPreset(preset);
                        RequestCloseSheet();
                    });
                choice.tooltip = "Choose Taggi pose " + (index + 1);
                choice.style.flexGrow = 1f;
                choice.style.marginLeft = 12f;
                row.Add(choice);
            }
            Text(content, "After choosing, place Taggi on a surface. Pinch to resize and twist to rotate.",
                14, false, Muted).style.marginTop = 12f;
        }

        private void BuildNoteSheet(VisualElement content, AppState state)
        {
            Text(content, "Leave a clue, then the whole story.", 16, false, Muted);
            DraftField(content, "Place", draftPlace, 80, false, value => draftPlace = value,
                "Name the place you are standing at.");
            DraftField(content, "Clue", draftTeaser, 180, false, value => draftTeaser = value,
                "Visitors see this before they find Taggi.");
            DraftField(content, "Your note", draftNote, 2000, true, value => draftNote = value,
                "Unlocked only when someone taps Taggi in AR.");
            if (!SignedIn(state))
            {
                Text(content, "Sign in to publish. Your draft will stay here.", 14, false, Muted).style.marginTop = 12f;
                sheetSubmitButton = Action(content, "Sign in to publish", OpenSignIn);
            }
            else
            {
                publishButton = Action(content, "Publish sticker", () =>
                {
                    if (!PaperFlow.CanPresentPublish(draftPlace, draftTeaser, draftNote,
                        controller.Ar?.CanPublish ?? false, controller.State.busy, controller.State.hasPendingPublication)) return;
                    publicationRequested = true;
                    controller.SetDraft(draftPlace, draftTeaser, draftNote);
                    controller.Publish();
                });
                publishButton.style.marginTop = 14f;
                if (state.hasPendingPublication)
                    Text(content, "Your saved placement is ready to retry. Your note is still here.", 13, false, Muted);
                else Text(content, "Publishing is available once tracking and your location are ready.", 13, false, Muted);
            }
        }

        private void RefreshSheet(AppState state)
        {
            if (sheetView == null || sheet == Sheet.None) return;
            sheetView.style.bottom = SheetBottom();
            ApplySheetHeight();
            if (sheet == Sheet.Note) RefreshPublish(state);
            if (sheet == Sheet.Collected) RefreshCollectedDetail(state);
            if (sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy);
            foreach (PaperSelection choice in reportChoices)
                choice.SetSelected(choice.userData is string reason && reason == selectedReportReason);
            if (sheet == Sheet.Report && sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy || !SignedIn(state));
            if (sheet == Sheet.Block && sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy || !SignedIn(state));
            if (sheet == Sheet.Withdraw && sheetSubmitButton != null) SetDisabled(sheetSubmitButton, state.busy);
            UpdateStatus(state);
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
            float pixels = Mathf.Max(Screen.safeArea.yMin, lastKeyboardHeight);
            return pixels * (root != null && root.layout.height > 0f && Screen.height > 0 ? root.layout.height / Screen.height : 1f);
        }

        private void ApplySheetHeight()
        {
            if (sheetView == null || root == null) return;
            if (root.layout.height <= 0f || Screen.height <= 0)
            {
                sheetView.style.maxHeight = Length.Percent(82f);
                return;
            }
            float scale = root.layout.height / Screen.height;
            float topInset = (Screen.height - Screen.safeArea.yMax) * scale;
            sheetView.style.maxHeight = Mathf.Max(180f, root.layout.height - SheetBottom() - topInset - 8f);
        }

        private void BuildCollectedDetail(VisualElement content, AppState state)
        {
            CollectedSticker sticker = state.detail;
            if (sticker == null || sticker.id != sheetStickerId)
            {
                Text(content, state.busy ? "Opening your sticker…" : "This sticker is unavailable in your collection.", 16, false, Muted);
                return;
            }
            Image artwork = Art(content, sticker.presetId, 140f);
            if (pendingCollectionCommitId == sticker.id)
            {
                pendingCollectionCommitId = null;
                artwork.schedule.Execute(() => { if (artwork.panel != null) PaperMotion.Commit(artwork); });
            }
            Text(content, Safe(sticker.place, "A place you visited"), 25, true).style.marginTop = 8f;
            Text(content, "Left by " + Safe(sticker.authorName, "someone nearby"), 14, false, Muted).style.marginTop = 4f;
            Text(content, "Collected " + Date(sticker.collectedAt), 13, false, Muted).style.marginTop = 2f;
            Divider(content);
            Text(content, "The clue", 14, true);
            Text(content, Safe(sticker.teaser, "No clue available"), 16).style.marginTop = 5f;
            Divider(content);
            Text(content, "The note", 14, true);
            Text(content, sticker.unavailable ? "This note is no longer available." : Safe(sticker.note, "No note available."), 17).style.marginTop = 7f;
            VisualElement actions = Row(content);
            actions.style.marginTop = 20f;
            Action(actions, "Report", () => OpenReport(sticker.id), false);
            if (!string.IsNullOrEmpty(sticker.authorId)) Action(actions, "Block author", () => OpenBlock(sticker.authorId), false);
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
            if (sheet == Sheet.Collected && controller.State.detail != null) controller.CloseDetail();
            sheet = Sheet.None;
            sheetStickerId = null;
            sheetAuthorId = null;
            sheetView = null;
            publishButton = null;
            QueueRender();
        }
    }
}
