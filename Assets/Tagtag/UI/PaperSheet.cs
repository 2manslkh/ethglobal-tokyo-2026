using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    // Shared JPEG-style sheet: one downward gesture owns dismissal; content keeps
    // upward drags and all drags that start away from the top of its scroll range.
    public sealed class PaperSheet : VisualElement
    {
        public ScrollView Scroll { get; }
        public VisualElement Grip { get; }
        private readonly Action closed;
        private readonly bool reducedMotion;
        private readonly Action returnFocus;
        private VisualElement focusRoot,previousFocus;
        private bool keyboardUsed;
        private int pointer=-1;
        private Vector2 origin;
        private float offset,lastY,velocity;
        private long lastTime;
        private bool eligible,dragging,closing,completed;
        private VisualElement scrim;
        private bool QuietMotion => reducedMotion || PaperMotion.Reduced(this) || PaperMotion.IsSuspended;

        public PaperSheet(string title,Action closed,bool reducedMotion,Action returnFocus=null,string dismissLabel="Done")
        {
            this.closed=closed;this.reducedMotion=reducedMotion;this.returnFocus=returnFocus;
            AddToClassList("drawer");AddToClassList("paper-sheet");name="tagtag-bottom-sheet";
            Grip=new VisualElement{name="sheet-grip"};Grip.AddToClassList("sheet-grip");Add(Grip);
            var handle=new VisualElement{pickingMode=PickingMode.Ignore};handle.AddToClassList("drawer-handle");Grip.Add(handle);
            var heading = new VisualElement(); heading.AddToClassList("sheet-heading"); Add(heading);
            var titleLabel = new Label(title); titleLabel.AddToClassList("sheet-title"); heading.Add(titleLabel);
            if (!string.IsNullOrEmpty(dismissLabel)) heading.Add(new PaperButton(dismissLabel, Dismiss, PaperButtonKind.Quiet));
            Scroll=new ScrollView{verticalScrollerVisibility=ScrollerVisibility.Hidden,horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            Scroll.AddToClassList("drawer-scroll");Add(Scroll);
            RegisterCallback<AttachToPanelEvent>(_=>
            {
                PaperMotion.SuspensionChanged+=MotionSuspended;
                bool refresh=false;
                schedule.Execute(()=>
                {
                    if(panel==null||closing)return;
                    scrim=parent?.Q(className:"scrim");
                    if(refresh)return;
                    if(scrim!=null)PaperMotion.Reveal(scrim,0);
                    if(!QuietMotion)PaperMotion.Reveal(this,32);
                });
                focusRoot=panel.visualTree;previousFocus=panel.focusController.focusedElement as VisualElement;
                keyboardUsed=true;
                focusRoot.RegisterCallback<KeyDownEvent>(ModalKey,TrickleDown.TrickleDown);
                focusRoot.RegisterCallback<FocusInEvent>(ModalFocus,TrickleDown.TrickleDown);
                schedule.Execute(()=>
                {
                    if(!keyboardUsed||closing||panel==null)return;
                    if(refresh&&panel.focusController.focusedElement is VisualElement restored&&Contains(restored))return;
                    Focusable().FirstOrDefault()?.Focus();
                });
            });
            RegisterCallback<DetachFromPanelEvent>(_=>
            {
                PaperMotion.SuspensionChanged-=MotionSuspended;
                Reset();focusRoot?.UnregisterCallback<KeyDownEvent>(ModalKey,TrickleDown.TrickleDown);
                focusRoot?.UnregisterCallback<FocusInEvent>(ModalFocus,TrickleDown.TrickleDown);focusRoot=null;
            });
        }
        // Bind after content has been added; Clickable/ScrollView capture bypasses ancestors.
        public void BindGestures()
        {
            var inputs=new HashSet<VisualElement>{this,Grip,Scroll,Scroll.contentViewport,Scroll.contentContainer};
            this.Query<Button>().ForEach(b=>inputs.Add(b));
            RegisterCallback<PointerDownEvent>(Down,TrickleDown.TrickleDown);
            foreach(var input in inputs)
            {
                input.RegisterCallback<PointerMoveEvent>(Move,TrickleDown.TrickleDown);
                input.RegisterCallback<PointerUpEvent>(Up,TrickleDown.TrickleDown);
                input.RegisterCallback<PointerCancelEvent>(e=>{if(e.pointerId==pointer)Reset();},TrickleDown.TrickleDown);
            }
            RegisterCallback<PointerCaptureOutEvent>(e=>{if(e.target==this&&e.pointerId==pointer)Reset();});
        }
        private void Down(PointerDownEvent e)
        {
            if(closing||e.button!=0)return;
            PaperMotion.Cancel(this,"reveal");PaperMotion.Cancel(this,"sheet");
            keyboardUsed=false;
            // A scroll/horizontal gesture can finish outside the sheet. A new
            // primary contact starts fresh; additional fingers cancel the pull.
            if(pointer!=-1){bool extra=pointer!=e.pointerId;Reset();if(extra)return;}
            if(!e.isPrimary)return;
            pointer=e.pointerId;origin=e.position;lastY=origin.y;lastTime=e.timestamp;velocity=offset=0;
            var target=e.target as VisualElement;
            eligible=target==null||!Scroll.Contains(target)||Scroll.scrollOffset.y<=.5f;
        }
        private void Move(PointerMoveEvent e)
        {
            if(e.pointerId!=pointer||!eligible)return;
            var delta=(Vector2)e.position-origin;
            if(!dragging)
            {
                if(delta.magnitude<12)return;
                if(delta.y<=0||delta.y<Mathf.Abs(delta.x)*1.25f){eligible=false;return;}
                dragging=true;AddToClassList("is-dragging");
                // Taking capture cancels Clickable. A synthetic cancel here is
                // queued by UI Toolkit and would cancel the new sheet gesture too.
                this.CapturePointer(pointer);
            }
            if(e.timestamp>lastTime)velocity=(e.position.y-lastY)*1000/(e.timestamp-lastTime);
            lastTime=e.timestamp;lastY=e.position.y;offset=Mathf.Max(0,delta.y);
            style.translate=new Translate(0,offset);e.StopImmediatePropagation();
        }
        public static bool ShouldDismiss(float height, float distance, float velocity)
        { return distance >= Mathf.Clamp(height * .22f, 64f, 150f) || (distance >= 24f && velocity > 900f); }
        private void Up(PointerUpEvent e)
        {
            if(e.pointerId!=pointer)return;
            bool held=dragging;
            bool dismiss=held&&ShouldDismiss(layout.height,offset,velocity);
            Reset(!dismiss);
            if(!held)return;
            e.StopImmediatePropagation();if(dismiss)Dismiss();
        }
        private void Reset(bool settle=true)
        {
            int id=pointer;pointer=-1;dragging=eligible=false;RemoveFromClassList("is-dragging");
            if(id!=-1&&this.HasPointerCapture(id))this.ReleasePointer(id);
            if(!closing&&settle)
            {
                float from=style.translate.value.y.value;
                if(QuietMotion||panel==null)style.translate=new Translate(0,0);
                else PaperMotion.Tween(this,"sheet",.24f,t=>style.translate=new Translate(0,from*(1-PaperMotion.EaseOut(t))));
            }
        }
        private List<VisualElement> Focusable()=>this.Query<VisualElement>().ToList()
            .Where(element => (element is Button || element is TextField || element is Toggle) &&
                element.enabledInHierarchy && element.resolvedStyle.display != DisplayStyle.None).ToList();
        private void ModalKey(KeyDownEvent e)
        {
            if(closing)return;keyboardUsed=true;
            if(e.keyCode==KeyCode.Escape){Dismiss();e.StopImmediatePropagation();return;}
            if(e.keyCode!=KeyCode.Tab)return;
            var controls=Focusable();if(controls.Count==0)return;
            var focused=panel.focusController.focusedElement as VisualElement;
            while(focused!=null&&!controls.Contains(focused))focused=focused.parent;
            int index=controls.IndexOf(focused);
            int next=(index+(e.shiftKey?-1:1)+controls.Count)%controls.Count;
            controls[next].Focus();if(Scroll.Contains(controls[next]))Scroll.ScrollTo(controls[next]);
            e.StopImmediatePropagation();
        }
        private void ModalFocus(FocusInEvent e)
        {if(keyboardUsed&&!closing&&e.target is VisualElement target&&target!=this&&!Contains(target))Focusable().FirstOrDefault()?.Focus();}
        private void FinishClose()
        {
            if(completed)return;completed=true;
            closed?.Invoke();
            if(!keyboardUsed)return;
            if(returnFocus!=null)returnFocus();else if(previousFocus?.panel!=null)previousFocus.Focus();
        }
        private void MotionSuspended(bool suspended)
        {
            if(!suspended)return;
            if(closing&&panel!=null)FinishClose();else Reset();
        }
        public void CompletePendingDismissal()
        {if(closing&&!completed&&panel!=null)FinishClose();}
        public void Dismiss()
        {
            if(closing)return;
            PaperMotion.Cancel(this,"reveal");
            float from=style.translate.value.y.value;
            PaperMotion.Cancel(this,"sheet");closing=true;Reset(false);
            if(QuietMotion||panel==null){FinishClose();return;}
            SetEnabled(false);
            float to=Mathf.Max(1,layout.height)+32;
            if(scrim!=null)PaperMotion.Tween(scrim,"reveal",PaperMotion.Exit,t=>scrim.style.opacity=1-PaperMotion.EaseOut(t));
            PaperMotion.Tween(this,"sheet",PaperMotion.Exit,t=>style.translate=new Translate(0,Mathf.Lerp(from,to,PaperMotion.EaseOut(t))));
            // Closing is already accepted input. Complete it after backgrounding too,
            // while detach pauses this scheduled item and cannot invoke a stale owner.
            schedule.Execute(FinishClose).ExecuteLater((long)(PaperMotion.Exit*1000));
        }
    }
}
