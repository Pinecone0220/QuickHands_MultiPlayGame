using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private readonly struct HoverInfo
        {
            public readonly string Title,Body;
            public HoverInfo(string title,string body){Title=title;Body=body;}
        }
        private readonly Dictionary<VisualElement,Func<HoverInfo>> hoverTips=new Dictionary<VisualElement,Func<HoverInfo>>();
        private VisualElement hoverTip,hoverTarget;
        private Label hoverTipTitle,hoverTipBody;
        private double hoverTipAt;
        private void BindHoverTip(VisualElement target,Func<HoverInfo> info)=>hoverTips.Add(target,info);
        private void BuildHoverTip()
        {
            hoverTip=Box(root,"hover-tip");hoverTip.pickingMode=PickingMode.Ignore;
            hoverTipTitle=Text(hoverTip,"","hover-tip-title");hoverTipBody=Text(hoverTip,"","hover-tip-body");
            hoverTipTitle.pickingMode=hoverTipBody.pickingMode=PickingMode.Ignore;
            Show(hoverTip,false);
        }
        private static HoverInfo WeaponHoverInfo(DuelWeapon weapon)
        {
            string body=weapon==DuelWeapon.BAS?"레버액션 · 3배율 스코프\n머리만 피해 · 목숨 2개 차감":weapon==DuelWeapon.SawedOff?
                "대난투: 빠른 준비 · 정조준 없음\n직격 75 · 양옆 25":$"대난투: 부분 장전 · 최대 6발\n머리 {WeaponRules.Damage(weapon,DuelHitPart.Head)} · 몸통 {WeaponRules.Damage(weapon,DuelHitPart.Body)} · 다리 {WeaponRules.Damage(weapon,DuelHitPart.Legs)}";
            return new HoverInfo(WeaponRules.Name(weapon),body);
        }
        private static HoverInfo ItemHoverInfo(DuelItem item)=>new HoverInfo(DuelItemArt.Name(item),item==DuelItem.Recovery?
            "체력 50 회복 · 소모성":item==DuelItem.NoruFace?"이번 사격 턴 테이저 피해·감전 면역\n2게임 쿨타임":item==DuelItem.Bullet?"사격자 전용 · 발사 1회 추가\n2개 지급 · 한 턴에 1개 · 소모성":item==DuelItem.Ink?"화면 대부분을 최대 5초 가립니다.\n소모성":"피해 5 · 최대 2초 감전\n2게임 쿨타임");
        private void LateUpdate()
        {
            UpdateHoverTip();UpdateInkLayering();
            // Above the rendered world, below every UI panel (including world-space name labels).
            crosshair?.SendToBack();
        }
        private void UpdateHoverTip()
        {
            if(hoverTip==null)return;
            if(!DuelPointer.HasPlayer||!DuelPointer.AltHeld||Mouse.current==null||root.panel==null)
            {hoverTarget=null;Show(hoverTip,false);return;}
            var mouse=Mouse.current.position.ReadValue();
            var point=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(mouse.x,Screen.height-mouse.y));
            var target=root.panel.Pick(point);
            // Picking also finds disabled selections; reading a hint must not activate a control.
            while(target!=null&&!hoverTips.ContainsKey(target))target=target.parent;
            if(target!=hoverTarget){hoverTarget=target;hoverTipAt=Time.realtimeSinceStartupAsDouble+.18;Show(hoverTip,false);}
            if(target==null||Time.realtimeSinceStartupAsDouble<hoverTipAt){Show(hoverTip,false);return;}
            var info=hoverTips[target]();hoverTipTitle.text=info.Title;hoverTipBody.text=info.Body;
            Show(hoverTip,true);hoverTip.BringToFront();
            point=root.WorldToLocal(point);
            float width=hoverTip.resolvedStyle.width,height=hoverTip.resolvedStyle.height;
            if(!(width>0))width=280;if(!(height>0))height=100;
            float x=point.x+16,y=point.y+20;
            if(x+width>root.resolvedStyle.width-8)x=point.x-width-16;
            if(y+height>root.resolvedStyle.height-8)y=point.y-height-12;
            hoverTip.style.left=Mathf.Clamp(x,8,Mathf.Max(8,root.resolvedStyle.width-width-8));
            hoverTip.style.top=Mathf.Clamp(y,8,Mathf.Max(8,root.resolvedStyle.height-height-8));
        }
    }
}
