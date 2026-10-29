using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        [Serializable] private sealed class PatchNoteEntry {public string date;public string[] notes;}
        [Serializable] private sealed class PatchNoteDocument {public PatchNoteEntry[] entries;}
        private VisualElement patchNotesLayer;
        private Button patchNotesClose;
        private Toggle patchNotesHideToday;
        private bool patchNotesOpen;
        private const string PatchNotesHiddenDateKey="PatchNotesHiddenDate";
        private static string PatchNotesToday()=>DateTime.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        private void BuildPatchNotes()
        {
            patchNotesLayer=Box(root,"weapon-info-layer");patchNotesLayer.name="patch-notes-layer";
            var panel=Box(patchNotesLayer,"patch-notes-panel");
            var heading=Box(panel,"weapon-info-heading");Text(heading,"패치노트","weapon-info-title");
            patchNotesClose=Button(heading,"닫기",()=>SetPatchNotes(false),"secondary");patchNotesClose.name="patch-notes-close";
            var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("patch-notes-scroll");panel.Add(scroll);
            PatchNoteDocument document=null;
            try{var source=Resources.Load<TextAsset>("QuickHandsPatchNotes");if(source!=null)document=JsonUtility.FromJson<PatchNoteDocument>(source.text);}
            catch(ArgumentException){Debug.LogWarning("패치노트 데이터를 읽지 못했습니다.");}
            if(document?.entries==null||document.entries.Length==0)Text(scroll,"표시할 패치 내역이 없습니다.","patch-note-text");
            else
            {
                Array.Sort(document.entries,(a,b)=>string.Compare(b?.date,a?.date,StringComparison.Ordinal));
                foreach(var entry in document.entries)
                {
                    if(entry==null||entry.notes==null)continue;
                    string date=DateTime.TryParseExact(entry.date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed.ToString("yyyy.MM.dd",CultureInfo.InvariantCulture):entry.date;
                    Text(scroll,date,"patch-note-date");
                    foreach(string note in entry.notes)
                    {
                        if(string.IsNullOrWhiteSpace(note))continue;
                        var card=Box(scroll,"patch-note-card");var label=Text(card,note,"patch-note-text");label.enableRichText=false;
                    }
                }
            }
            var footer=Box(panel,"patch-notes-footer");
            patchNotesHideToday=new Toggle("오늘 하루 표시 안 함");
            patchNotesHideToday.name="patch-notes-hide-today";
            DuelPointer.EnableAltClick(patchNotesHideToday);
            footer.Add(patchNotesHideToday);
            root.RegisterCallback<KeyDownEvent>(e=>
            {if(patchNotesOpen&&e.keyCode==KeyCode.Escape){SetPatchNotes(false);e.StopImmediatePropagation();}},TrickleDown.TrickleDown);
            SetPatchNotes(PlayerPrefs.GetString(PatchNotesHiddenDateKey,"")!=PatchNotesToday());
        }
        private void SetPatchNotes(bool visible)
        {
            if(visible&&manager.Busy)return;
            if(visible)
            {
                if(weaponInfoOpen)SetWeaponInfo(false);
                if(quitConfirmOpen)SetQuitConfirmation(false);
                patchNotesHideToday.SetValueWithoutNotify(PlayerPrefs.GetString(PatchNotesHiddenDateKey,"")==PatchNotesToday());
            }
            else if(patchNotesOpen)
            {
                if(patchNotesHideToday.value)PlayerPrefs.SetString(PatchNotesHiddenDateKey,PatchNotesToday());
                else PlayerPrefs.DeleteKey(PatchNotesHiddenDateKey);
                PlayerPrefs.Save();
            }
            patchNotesOpen=visible;Show(patchNotesLayer,visible);
            if(visible){patchNotesLayer.BringToFront();root.schedule.Execute(()=>{if(patchNotesOpen)patchNotesClose.Focus();});}
        }
        private void UpdatePatchNotes()
        {if(!patchNotesOpen)return;if(manager.Busy)SetPatchNotes(false);else patchNotesLayer.BringToFront();}
    }
}
