using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private void BuildAccount(AppState state)
        {
            VisualElement heading = Row(safeRoot);
            heading.style.height = 62f;
            heading.style.paddingLeft = 16f;
            heading.style.paddingRight = 20f;
            heading.style.alignItems = Align.Center;
            Action(heading, "Back", () =>
            {
                if (accountScreen == AccountScreen.Overview || accountScreen == AccountScreen.SignIn)
                {
                    controller.SetAccountOpen(false);
                }
                else
                {
                    accountScreen = AccountScreen.Overview;
                    QueueRender();
                }
            }, false);
            Text(heading, accountScreen == AccountScreen.SignIn ? "Sign in" : accountScreen == AccountScreen.Authored ? "Your stickers" : accountScreen == AccountScreen.DeleteConfirmation ? "Delete account" : "Account", 21, true);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.paddingLeft = 24f;
            scroll.style.paddingRight = 24f;
            safeRoot.Add(scroll);
            VisualElement content = scroll.contentContainer;

            if (accountScreen == AccountScreen.SignIn && !SignedIn(state)) BuildSignIn(content, state);
            else if (accountScreen == AccountScreen.Authored) BuildAuthored(content, state);
            else if (accountScreen == AccountScreen.DeleteConfirmation) BuildDeleteConfirmation(content, state);
            else BuildAccountOverview(content, state);
            AddStatus(content, state);
        }

        private void BuildSignIn(VisualElement content, AppState state)
        {
            Text(content, "Keep the stickers you find", 29, true).style.marginTop = 26f;
            Label explanation = Text(content, "Explore freely. Sign in to leave a sticker or add one to your book. Your collection follows this account.", 16, false, Muted);
            explanation.style.marginTop = 12f;
            explanation.style.marginBottom = 24f;
            Button apple = Action(content, "Continue with Apple", () => controller.SignIn("apple"));
            apple.style.marginBottom = 10f;
            SetDisabled(apple, state.busy || !state.servicesConfigured);
            Button google = Action(content, "Continue with Google", () => controller.SignIn("google"));
            SetDisabled(google, state.busy || !state.servicesConfigured);
            if (!state.servicesConfigured)
            {
                Text(content, "Sign-in is unavailable until the service is configured.", 14, false, Muted).style.marginTop = 12f;
            }
            Action(content, "Continue exploring", () => controller.SetAccountOpen(false), false).style.marginTop = 20f;
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
            Text(collection, CollectionPresentation.OrderedDistinct(state.collection).Count.ToString(), 17, false, Muted);
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
            Text(authored, state.authored.Count.ToString(), 17, false, Muted);
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
            AddTextSize(sizes, "Standard", 1f);
            AddTextSize(sizes, "Larger", 1.2f);
            AddTextSize(sizes, "Largest", 1.4f);
            Button motion = Action(content, reducedMotion ? "Reduced motion: on" : "Reduced motion: off", () =>
            {
                reducedMotion = !reducedMotion;
                PlayerPrefs.SetInt("tagtag.reducedMotion", reducedMotion ? 1 : 0);
                PlayerPrefs.Save();
                QueueRender();
            }, false);
            motion.style.alignSelf = Align.FlexStart;
            motion.style.marginTop = 12f;
            Text(content, "Page changes stay still. AR movement follows your camera.", 13, false, Muted);
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
            Button button = Action(parent, label, () =>
            {
                textScale = scale;
                PlayerPrefs.SetFloat("tagtag.textScale", scale);
                PlayerPrefs.Save();
                QueueRender();
            }, Mathf.Abs(textScale - scale) < 0.01f);
            button.style.flexGrow = 1f;
            button.style.marginRight = 4f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
        }

        private void BuildAuthored(VisualElement content, AppState state)
        {
            Text(content, "Stickers you left", 27, true).style.marginTop = 22f;
            Text(content, "Withdraw a sticker to stop new discoveries. Copies already collected remain in other books.", 14, false, Muted).style.marginTop = 8f;
            if (state.authored.Count == 0)
            {
                Text(content, "You have not left a sticker yet.", 16, false, Muted).style.marginTop = 28f;
                Action(content, "Open STICK", () =>
                {
                    controller.SetAccountOpen(false);
                    controller.Navigate(AppPage.Stick);
                }).style.marginTop = 15f;
                return;
            }

            foreach (StickerSummary sticker in state.authored)
            {
                if (sticker == null || string.IsNullOrEmpty(sticker.id)) continue;
                Divider(content);
                VisualElement row = Row(content);
                row.style.alignItems = Align.Center;
                Art(row, sticker.presetId, 62f);
                VisualElement words = Column(row);
                words.style.flexGrow = 1f;
                words.style.marginLeft = 12f;
                Text(words, Safe(sticker.place, "A place"), 17, true);
                Text(words, Safe(sticker.teaser, "No clue"), 13, false, Muted);
                Text(words, "Left " + Date(sticker.createdAt), 12, false, Muted);
                string stickerId = sticker.id;
                Button withdraw = Action(content, "Withdraw this sticker", () =>
                {
                    sheet = Sheet.Withdraw;
                    sheetStickerId = stickerId;
                    QueueRender();
                }, false);
                withdraw.style.alignSelf = Align.FlexStart;
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
            TextField confirmation = new TextField();
            confirmation.name = "Delete confirmation";
            confirmation.tooltip = "Type DELETE to confirm account deletion";
            confirmation.value = deleteConfirmation;
            confirmation.style.minHeight = 44f;
            confirmation.style.fontSize = Mathf.RoundToInt(17f * textScale);
            confirmation.RegisterCallback<FocusInEvent>(_ => focusedField = confirmation);
            confirmation.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (focusedField == confirmation) focusedField = null;
                QueueRender();
            });
            content.Add(confirmation);
            Button delete = Action(content, "Permanently delete account", () =>
            {
                if (deleteConfirmation != "DELETE") return;
                controller.DeleteAccount();
                deleteConfirmation = "";
            });
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

        private void BuildSheet(AppState state)
        {
            VisualElement scrim = new VisualElement();
            scrim.name = "Sheet scrim";
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0f;
            scrim.style.top = 0f;
            scrim.style.right = 0f;
            scrim.style.bottom = 0f;
            scrim.style.backgroundColor = new Color(0f, 0f, 0f, 0.26f);
            root.Add(scrim);
            VisualElement panel = Column(scrim);
            panel.style.position = Position.Absolute;
            panel.style.left = 0f;
            panel.style.right = 0f;
            panel.style.bottom = Mathf.Max(0f, Screen.safeArea.yMin * (root.layout.height > 0f ? root.layout.height / Screen.height : 1f));
            panel.style.paddingTop = 18f;
            panel.style.paddingBottom = 20f;
            panel.style.paddingLeft = 24f;
            panel.style.paddingRight = 24f;
            panel.style.backgroundColor = Paper;
            panel.style.borderTopLeftRadius = 22f;
            panel.style.borderTopRightRadius = 22f;
            panel.style.maxHeight = Length.Percent(78);
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            panel.Add(scroll);
            VisualElement content = scroll.contentContainer;
            VisualElement top = Row(content);
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            Text(top, sheet == Sheet.Collected ? "Collected sticker" : sheet == Sheet.Report ? "Report sticker" : sheet == Sheet.Withdraw ? "Withdraw sticker" : "Block author", 21, true);
            Action(top, "Close", CloseSheet, false);
            Divider(content);
            if (sheet == Sheet.Collected) BuildCollectedDetail(content, state);
            else if (sheet == Sheet.Report) BuildReportSheet(content, state);
            else if (sheet == Sheet.Withdraw) BuildWithdrawSheet(content, state);
            else BuildBlockSheet(content, state);
        }

        private void BuildCollectedDetail(VisualElement content, AppState state)
        {
            CollectedSticker sticker = state.detail;
            if (sticker == null || sticker.id != sheetStickerId)
            {
                Text(content, state.busy ? "Opening your sticker…" : "This sticker is unavailable in your collection.", 16, false, Muted);
                return;
            }
            Art(content, sticker.presetId, 140f);
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
                Button button = Action(content, (selectedReportReason == reason ? "Selected: " : "") + reason, () =>
                {
                    selectedReportReason = choice;
                    QueueRender();
                }, selectedReportReason == reason);
                button.style.marginTop = 8f;
            }
            Button submit = Action(content, "Send report", () =>
            {
                controller.Report(sheetStickerId, selectedReportReason);
                CloseSheet();
            });
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
                CloseSheet();
            });
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
                CloseSheet();
            });
            withdraw.style.marginTop = 18f;
            SetDisabled(withdraw, state.busy);
        }

        private void CloseSheet()
        {
            if (controller.State.detail != null) controller.CloseDetail();
            sheet = Sheet.None;
            sheetStickerId = null;
            sheetAuthorId = null;
            QueueRender();
        }
    }
}
