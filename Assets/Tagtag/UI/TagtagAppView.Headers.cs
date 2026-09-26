using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    public sealed partial class TagtagAppView
    {
        private VisualElement headerStringsHost;
        private PaperHangingHeader pageHeader;

        private PaperHangingHeader HangingHeader(VisualElement parent, string title, int size = 30)
        {
            pageHeader = new PaperHangingHeader(title, headerStringsHost);
            int pointSize = PaperTypography.PointSize(size, PaperTextRole.Display);
            pageHeader.Title.userData = pointSize;
            pageHeader.Title.style.unityFont = DisplayFont;
            pageHeader.Title.style.unityFontStyleAndWeight = FontStyle.Normal;
            pageHeader.Title.style.fontSize = Mathf.RoundToInt(pointSize * textScale);
            pageHeader.Title.style.color = Ink;
            parent.Add(pageHeader);
            return pageHeader;
        }

        private void EnterPageHeader(AppNavigationReason reason)
        {
            if (pageHeader == null)
            {
                PaperNavigationMotion.Enter(screenHost, reason);
                return;
            }
            // A fixed string anchor cannot inherit a translating page transition.
            PaperMotion.Cancel(screenHost, "navigation");
            screenHost.style.translate = new Translate(0, 0);
            screenHost.style.opacity = 1f;
            pageHeader.Enter(reason);
        }
    }
}
