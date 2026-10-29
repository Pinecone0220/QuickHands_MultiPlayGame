using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement gachaLayer,gachaWheel,gachaControls,gachaDetails,gachaLeft,gachaRight;
        private Label gachaTitle,skinHoverName,skinAwardName;
        private DuelGachaAudio gachaAudio; private DuelGachaWheel3D gachaWheel3D; private Image gachaWheelImage;
        private VisualElement awardHalo,awardFlash,awardCard; private readonly VisualElement[] awardSparks=new VisualElement[12]; private float awardStarted; private int awardGrade; private bool skipAwardAnimation;
        private VisualElement skinHoverPanel,skinAwardOverlay; private Image skinHoverImage,skinAwardImage; private DuelSkinPreview skinModelPreview; private int gachaTick;
        private Label gachaResult,gachaGoldLabel,gachaCreditLabel,exchangeCreditLabel; private Button gachaSkip,gachaOne,gachaTen;
        private VisualElement creditExchangeLayer;
        private Texture2D creditIcon;
        private ScrollView batchResults;
        private readonly int[] batchCategories=new int[10],batchSkins=new int[10];
        private readonly bool[] batchDuplicates=new bool[10];private int batchCount,batchCreditTotal;
        private const string GoldKey="qh.gacha.gold";
        private const string CreditKey="qh.gacha.credit";
        private static int GachaGold=>Mathf.Max(0,PlayerPrefs.GetInt(GoldKey,0));
        private static int GachaCredits=>Mathf.Max(0,PlayerPrefs.GetInt(CreditKey,0));
        private static int DuplicateCredit(int grade)
        {switch(grade){case 0:return 1;case 1:return 5;case 2:return 10;case 3:return 15;case 4:return 30;default:return 0;}}
        private static void AddGachaGold(int amount)
        {if(amount<=0)return;PlayerPrefs.SetInt(GoldKey,(int)System.Math.Min(int.MaxValue,(long)GachaGold+amount));PlayerPrefs.Save();}
        private void UpdateGachaGold()
        {
            if(gachaGoldLabel!=null)gachaGoldLabel.text="보유 골드  "+GachaGold.ToString("N0")+"원";
            if(gachaCreditLabel!=null)gachaCreditLabel.text="보유 크레딧  "+GachaCredits.ToString("N0");
            if(exchangeCreditLabel!=null)exchangeCreditLabel.text="보유 크레딧  "+GachaCredits.ToString("N0");
            if(gachaOne!=null)gachaOne.SetEnabled(GachaGold>=10&&!gachaSpinning);
            if(gachaTen!=null)
            {
                int count=Mathf.Min(10,GachaGold/10);
                gachaTen.text=count>=2?"최대 10회 뽑기 · "+count+"회 / "+(count*10)+"원":"최대 10회 뽑기 · 최소 20원";
                gachaTen.SetEnabled(count>=2&&!gachaSpinning);
            }
        }
        private readonly Texture2D[,] skinPreviews=new Texture2D[DuelLoadout.ItemCount,DuelSkins.Colors+1]; private bool gachaOpen,gachaSpinning,inventoryOpen,awaitingGachaBall;
        private readonly bool[] inventoryExpanded=new bool[DuelLoadout.ItemCount];
        private readonly bool[] weaponInventoryExpanded=new bool[3];
        private readonly Texture2D[,] weaponSkinPreviews=new Texture2D[3,DuelSkins.WeaponSkinCount+1];
        private const int PrizeCount=DuelLoadout.ItemCount*DuelSkins.Colors+3*DuelSkins.WeaponSkinCount;
        private static int PrizeGrade(int category,int skin)=>category<DuelLoadout.ItemCount?DuelSkins.Grade(skin):DuelSkins.WeaponGrade(skin);
        private static string PrizeName(int category,int skin)=>category<DuelLoadout.ItemCount?
            DuelItemArt.Name((DuelItem)category)+" "+DuelSkins.ColorName(skin):
            WeaponRules.Name(WeaponRules.Selectable[category-DuelLoadout.ItemCount])+" "+DuelSkins.WeaponSkinName(skin);
        private void ShowPrizeModel(int category,int skin)
        {if(category<DuelLoadout.ItemCount)skinModelPreview.Show((DuelItem)category,skin);else skinModelPreview.ShowWeapon(WeaponRules.Selectable[category-DuelLoadout.ItemCount],skin);}
        private static string PrizeChance=>(100f/PrizeCount).ToString("0.##")+"%";
        private float spinStart,spinFrom,spinTo,wheelAngle; private int prizeItem,prizeColor,spinSegment;
        private void RefreshSkinIcons()
        {
            for(int i=0;i<DuelLoadout.ItemCount;i++)if(itemIcons[i]!=null)DuelSkins.TintIcon(itemIcons[i],(DuelItem)i,DuelSkins.Equipped(i));
            if(weaponIcons[2]!=null)DuelSkins.TintIcon(weaponIcons[2],DuelItem.Taser,DuelSkins.Equipped((int)DuelItem.Taser));
        }
        private void BuildGacha()
        {
            gachaLayer=Box(root,"gacha-layer");
            var panel=Box(gachaLayer,"gacha-panel");
            var head=Box(panel,"gacha-head");gachaTitle=Text(head,"운을 시험하기","gacha-title");Button(head,"닫기",()=>SetGachaOpen(false),"secondary");
            var body=Box(panel,"gacha-body");var left=Box(body,"gacha-left");gachaLeft=left;
            var wheelFrame=Box(left,"gacha-wheel-frame");gachaWheel=Box(wheelFrame,"gacha-wheel");
            gachaWheelImage=new Image{scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};gachaWheelImage.AddToClassList("gacha-3d-image");gachaWheel.Add(gachaWheelImage);gachaWheel.RegisterCallback<ClickEvent>(_=>ThrowGachaBall());
            gachaResult=Text(left,"룰렛을 돌려 아이템 스킨을 획득하세요.","gacha-result");
            var right=Box(body,"gacha-right");gachaRight=right;gachaControls=Box(right,"gacha-controls");
            var goldRow=Box(gachaControls,"gacha-gold-row");
            if(goldCoinIcon==null)goldCoinIcon=MakeGoldCoinIcon();
            var goldIcon=new Image{image=goldCoinIcon,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};goldIcon.AddToClassList("gacha-gold-icon");goldRow.Add(goldIcon);
            gachaGoldLabel=Text(goldRow,"","gacha-gold");
            creditIcon=MakeCreditIcon();var creditImage=new Image{image=creditIcon,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};creditImage.AddToClassList("gacha-credit-icon");goldRow.Add(creditImage);
            gachaCreditLabel=Text(goldRow,"","gacha-credit");
            gachaOne=Button(gachaControls,"1회 뽑기 · 10원",()=>StartGachaSpin(1),"primary");
            gachaTen=Button(gachaControls,"최대 10회 뽑기",()=>{int count=Mathf.Min(10,GachaGold/10);if(count>=2)StartGachaSpin(count);},"primary");
            Text(gachaControls,"10회 뽑기: SR 이상 1개 보장","gacha-probability");
            Button(gachaControls,"크레딧 교환소",()=>SetCreditExchange(true),"secondary");UpdateGachaGold();



            gachaSkip=Button(right,"넘기기",()=>{skipAwardAnimation=true;FinishGachaSpin();},"secondary");Show(gachaSkip,false);
            gachaDetails=Box(right,"gacha-details");
            skinHoverPanel=Box(right,"skin-hover-panel");skinHoverName=Text(skinHoverPanel,"","skin-preview-name");skinHoverImage=new Image{scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};skinHoverImage.AddToClassList("skin-preview-image");skinHoverPanel.Add(skinHoverImage);Show(skinHoverPanel,false);
            skinAwardOverlay=Box(gachaLayer,"skin-award-overlay");awardHalo=Box(skinAwardOverlay,"skin-award-halo");awardHalo.pickingMode=PickingMode.Ignore;
            for(int i=0;i<awardSparks.Length;i++){awardSparks[i]=Text(skinAwardOverlay,"✦","skin-award-spark");awardSparks[i].pickingMode=PickingMode.Ignore;}
            awardFlash=Box(skinAwardOverlay,"skin-award-flash");awardFlash.pickingMode=PickingMode.Ignore;awardCard=Box(skinAwardOverlay,"skin-award-card");skinAwardName=Text(awardCard,"","skin-award-name");skinAwardImage=new Image{scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};skinAwardImage.AddToClassList("skin-award-image");awardCard.Add(skinAwardImage);
            batchResults=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};batchResults.AddToClassList("gacha-batch-results");awardCard.Add(batchResults);Show(batchResults,false);
            Text(awardCard,"화면을 클릭하여 닫기","skin-award-hint");skinAwardOverlay.RegisterCallback<ClickEvent>(e=>{CloseSkinPreview();e.StopPropagation();});Show(skinAwardOverlay,false);
            creditExchangeLayer=Box(gachaLayer,"credit-exchange-layer");var exchangePanel=Box(creditExchangeLayer,"credit-exchange-panel");
            var exchangeHeader=Box(exchangePanel,"gacha-head");Text(exchangeHeader,"크레딧 교환소","gacha-title");Button(exchangeHeader,"닫기",()=>SetCreditExchange(false),"secondary");
            exchangeCreditLabel=Text(exchangePanel,"","gacha-credit");Show(creditExchangeLayer,false);
            skinModelPreview=gameObject.AddComponent<DuelSkinPreview>();gachaAudio=gameObject.AddComponent<DuelGachaAudio>();gachaWheel3D=gameObject.AddComponent<DuelGachaWheel3D>();Show(gachaLayer,false);
        }
        private void SetGachaOpen(bool open,bool inventory=false)
        {
            if(open&&manager.Busy)return;
            if(!open&&gachaSpinning)FinishGachaSpin();
            if(!open)SetCreditExchange(false);
            inventoryOpen=open&&inventory;if(!open)CloseSkinPreview();else Show(skinHoverPanel,false);var music=GetComponent<DuelBackgroundMusic>();if(music!=null)music.GachaOpen=open&&!inventory;
            gachaOpen=open;Show(gachaLayer,open);gachaWheel3D.SetVisible(open&&!inventory);
            if(open)
            {
                if(!inventory)gachaWheelImage.image=gachaWheel3D.Texture;
                gachaLayer.BringToFront();gachaTitle.text=inventory?"스킨 보관함":"운을 시험하기";
                UpdateGachaGold();Show(gachaLeft,!inventory);Show(gachaControls,!inventory);
                if(inventory)gachaRight.AddToClassList("inventory-only");else gachaRight.RemoveFromClassList("inventory-only");
                if(inventory){ResetSkinHoverPreview();Show(skinHoverPanel,true);}
                ShowGachaDetails(inventory?2:1);
            }
        }
        private void OpenSkinInventory()
        {
            SetGachaOpen(true,true);
        }
        private void SetCreditExchange(bool open)
        {
            if(creditExchangeLayer==null)return;
            Show(creditExchangeLayer,open);
            if(open){UpdateGachaGold();creditExchangeLayer.BringToFront();}
        }
        private static Texture2D MakeCreditIcon()
        {
            const int size=64,samples=3;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Color pixel=Color.clear;
                for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
                {
                    float px=(x+(sx+.5f)/samples-size*.5f)/(size*.5f),py=(y+(sy+.5f)/samples-size*.5f)/(size*.5f);
                    float ax=Mathf.Abs(px),ay=Mathf.Abs(py),edge=Mathf.Max(Mathf.Max(ax,ay),(ax+ay)*.7071f);if(edge>.88f)continue;
                    Color color=edge>.77f?new Color(.27f,.38f,.40f):edge>.68f?new Color(.64f,.78f,.76f):Color.Lerp(new Color(.14f,.29f,.31f),new Color(.42f,.65f,.63f),Mathf.Clamp01(.55f+py*.3f-px*.15f));
                    if(edge>.56f&&edge<.59f)color=new Color(.20f,.37f,.37f);
                    float diamond=ax+ay;if(diamond<.33f&&(ax<.06f||ay<.06f||diamond<.14f))color=new Color(.84f,.94f,.86f);
                    pixel+=color/(samples*samples);
                }
                pixels[y*size+x]=pixel;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Quick Hands credit token",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};texture.SetPixels(pixels);texture.Apply();return texture;
        }
        private void ShowGachaDetails(int mode)
        {
            gachaDetails.Clear();
            Text(gachaDetails,mode==0?"확률 정보":mode==1?"상품 정보":"스킨 보관함","gacha-detail-title");
            if(mode==0)Text(gachaDetails,"전체 "+PrizeCount+"종 · 각 상품 약 "+PrizeChance+"\n중복 상품도 다시 획득할 수 있습니다.","gacha-description");
            else
            {
                if(mode==1)Text(gachaDetails,"1회 기준 전체 "+PrizeCount+"종 · 각 상품 약 "+PrizeChance+"\n10회에서 첫 9개에 SR 이상이 없으면 마지막은 SR 이상 중 균등 추첨","gacha-probability");
                var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.contentViewport.style.overflow=Overflow.Hidden;scroll.AddToClassList("gacha-list");gachaDetails.Add(scroll);
                var gradeLists=new VisualElement[5];
                if(mode==1)for(int grade=4;grade>=0;grade--)
                {
                    var heading=Text(scroll,DuelSkins.GradeName(grade),"gacha-item-heading");heading.style.color=DuelSkins.GradeColor(grade);
                    gradeLists[grade]=Box(scroll,"gacha-grade-items");
                }
                for(int item=0;item<DuelLoadout.ItemCount;item++)
                {
                    int itemIndex=item;var options=new Button[DuelSkins.Colors+1];VisualElement list=scroll;
                    if(mode==2)
                    {
                        Button heading=null;VisualElement contents=null;
                        heading=Button(scroll,(inventoryExpanded[item]?"▼  ":"▶  ")+DuelItemArt.Name((DuelItem)item),()=>
                        {
                            inventoryExpanded[itemIndex]=!inventoryExpanded[itemIndex];
                            heading.text=(inventoryExpanded[itemIndex]?"▼  ":"▶  ")+DuelItemArt.Name((DuelItem)itemIndex);
                            Show(contents,inventoryExpanded[itemIndex]);
                            if(!inventoryExpanded[itemIndex])ResetSkinHoverPreview();
                        },"secondary");
                        heading.AddToClassList("skin-inventory-heading");
                        contents=Box(scroll,"skin-inventory-items");Show(contents,inventoryExpanded[item]);list=contents;
                        var basicRow=Box(list,"gacha-product-row");
                        if(skinPreviews[item,0]==null)skinPreviews[item,0]=DuelItemArt.CreateIcon((DuelItem)item);
                        var basicIcon=new Image{image=skinPreviews[item,0],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};basicIcon.AddToClassList("gacha-product-icon");basicRow.Add(basicIcon);
                        var basic=Button(basicRow,DuelSkins.Equipped(item)==0?"기본 · 장착 중":"기본 장착",()=>EquipInventorySkin(itemIndex,0,options),"secondary");options[0]=basic;RegisterSkinHover(basicRow,itemIndex,0);
                    }
                    for(int c=1;c<=DuelSkins.Colors;c++)
                    {
                        int color=c,grade=DuelSkins.Grade(c);bool owned=DuelSkins.Owned(item,c);
                        if(mode==2&&!owned)continue;
                        string label=DuelItemArt.Name((DuelItem)item)+" "+DuelSkins.ColorName(c)+"  ·  "+DuelSkins.GradeName(grade);
                        var row=Box(mode==1?gradeLists[grade]:list,"gacha-product-row");
                        if(skinPreviews[item,c]==null){skinPreviews[item,c]=DuelItemArt.CreateIcon((DuelItem)item);DuelSkins.TintIcon(skinPreviews[item,c],(DuelItem)item,c);}
                        var preview=new Image{image=skinPreviews[item,c],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};preview.AddToClassList("gacha-product-icon");row.Add(preview);
                        if(mode==2){RegisterSkinHover(row,itemIndex,color);if(DuelSkins.Equipped(item)==c)label+="  ·  장착 중";var b=Button(row,label,()=>EquipInventorySkin(itemIndex,color,options),"secondary");options[color]=b;b.style.color=DuelSkins.GradeColor(grade);}
                        else {var line=Text(row,label+"  ·  "+PrizeChance,"gacha-product");line.style.color=DuelSkins.GradeColor(grade);}
                    }
                }
                for(int weapon=0;weapon<WeaponRules.Selectable.Length;weapon++)
                {
                    int w=weapon;var kind=WeaponRules.Selectable[w];var options=new Button[DuelSkins.WeaponSkinCount+1];VisualElement list=scroll;
                    if(mode==2)
                    {
                        Button heading=null;VisualElement contents=null;
                        heading=Button(scroll,(weaponInventoryExpanded[w]?"▼  ":"▶  ")+WeaponRules.Name(kind),()=>
                        {weaponInventoryExpanded[w]=!weaponInventoryExpanded[w];heading.text=(weaponInventoryExpanded[w]?"▼  ":"▶  ")+WeaponRules.Name(kind);Show(contents,weaponInventoryExpanded[w]);if(!weaponInventoryExpanded[w])ResetSkinHoverPreview();},"secondary");
                        heading.AddToClassList("skin-inventory-heading");contents=Box(scroll,"skin-inventory-items");Show(contents,weaponInventoryExpanded[w]);list=contents;
                    }
                    for(int skin=mode==2?0:1;skin<=DuelSkins.WeaponSkinCount;skin++)
                    {
                        int value=skin;if(mode==2&&!DuelSkins.WeaponOwned(w,skin))continue;
                        int grade=DuelSkins.WeaponGrade(skin);var row=Box(mode==1?gradeLists[grade]:list,"gacha-product-row");
                        if(weaponSkinPreviews[w,skin]==null)
                        {
                            var icon=MakeWeaponIcon(kind,true);
                            if(skin>0){var pixels=icon.GetPixels();for(int i=0;i<pixels.Length;i++)if(pixels[i].a>.01f){Color tint=skin==8?DuelSkins.FadeColor((i%icon.width)/(float)(icon.width-1)):DuelSkins.WeaponTint(skin);float v=pixels[i].grayscale;pixels[i]=new Color(tint.r*v,tint.g*v,tint.b*v,pixels[i].a);}icon.SetPixels(pixels);icon.Apply();}
                            weaponSkinPreviews[w,skin]=icon;
                        }
                        var preview=new Image{image=weaponSkinPreviews[w,skin],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};preview.AddToClassList("gacha-product-icon");row.Add(preview);
                        string label=skin==0?"기본":WeaponRules.Name(kind)+" "+DuelSkins.WeaponSkinName(skin)+"  ·  "+DuelSkins.GradeName(grade);
                        if(mode==2)
                        {
                            if(DuelSkins.WeaponEquipped(w)==skin)label+="  ·  장착 중";
                            var b=Button(row,label,()=>EquipWeaponSkin(w,value,options),"secondary");options[skin]=b;if(skin>0)b.style.color=DuelSkins.GradeColor(grade);
                            row.RegisterCallback<PointerEnterEvent>(_=>{ShowPrizeModel(DuelLoadout.ItemCount+w,value);skinHoverImage.image=skinModelPreview.Texture;Show(skinHoverImage,true);skinHoverName.text=WeaponRules.Name(kind)+(value==0?" · 기본":" "+DuelSkins.WeaponSkinName(value));Show(skinHoverPanel,true);});
                            row.RegisterCallback<PointerLeaveEvent>(_=>ResetSkinHoverPreview());
                        }
                        else {var line=Text(row,label+"  ·  "+PrizeChance,"gacha-product");line.style.color=DuelSkins.GradeColor(grade);}
                    }
                }
            }
        }
        private void EquipWeaponSkin(int weapon,int skin,Button[] options)
        {
            DuelSkins.EquipWeapon(weapon,skin);int selected=DuelSkins.WeaponEquipped(weapon);
            for(int i=0;i<options.Length;i++)if(options[i]!=null)
                options[i].text=(i==0?"기본":WeaponRules.Name(WeaponRules.Selectable[weapon])+" "+DuelSkins.WeaponSkinName(i)+"  ·  "+DuelSkins.GradeName(DuelSkins.WeaponGrade(i)))+(selected==i?"  ·  장착 중":"");
        }
        private void EquipInventorySkin(int item,int color,Button[] options)
        {
            DuelSkins.Equip(item,color);RefreshSkinIcons();int selected=DuelSkins.Equipped(item);
            if(options[0]!=null)options[0].text=selected==0?"기본 · 장착 중":"기본 장착";
            for(int c=1;c<options.Length;c++)if(options[c]!=null)
                options[c].text=DuelItemArt.Name((DuelItem)item)+" "+DuelSkins.ColorName(c)+"  ·  "+DuelSkins.GradeName(DuelSkins.Grade(c))+(selected==c?"  ·  장착 중":"");
        }
        private void RegisterSkinHover(VisualElement element,int item,int color)
        {
            element.RegisterCallback<PointerEnterEvent>(_=>{skinModelPreview.Show((DuelItem)item,color);skinHoverImage.image=skinModelPreview.Texture;Show(skinHoverImage,true);skinHoverName.text=DuelItemArt.Name((DuelItem)item)+(color==0?" · 기본":" "+DuelSkins.ColorName(color));Show(skinHoverPanel,true);});
            element.RegisterCallback<PointerLeaveEvent>(_=>ResetSkinHoverPreview());
        }
        private void ResetSkinHoverPreview()
        {if(skinHoverName!=null)skinHoverName.text="스킨에 마우스를 올려 미리 보기";if(skinHoverImage!=null){skinHoverImage.image=null;Show(skinHoverImage,false);}if(skinModelPreview!=null)skinModelPreview.Hide();}
        private void CloseSkinPreview()
        {if(skinAwardOverlay!=null)Show(skinAwardOverlay,false);ResetSkinHoverPreview();if(skinHoverPanel!=null)Show(skinHoverPanel,inventoryOpen);}
        private void ShowSkinAward(int item,int color,bool duplicate)
        {
            ShowPrizeModel(item,color);skinAwardImage.image=skinModelPreview.Texture;
            skinAwardName.text=DuelSkins.GradeName(PrizeGrade(item,color))+"  ·  "+PrizeName(item,color)+(duplicate?"  (중복 · 크레딧 +"+DuplicateCredit(PrizeGrade(item,color))+")":"  획득!");
            awardGrade=PrizeGrade(item,color);awardStarted=Time.unscaledTime-(skipAwardAnimation?2f:0f);skinAwardName.style.color=DuelSkins.GradeColor(awardGrade);Show(skinAwardOverlay,true);skinAwardOverlay.BringToFront();UpdateAwardAnimation();
        }
        private void SetGachaWheelAngle(float angle)
        {
            wheelAngle=angle;gachaWheel3D.SetAngle(angle);
        }
        private void UpdateAwardAnimation()
        {
            if(skinAwardOverlay==null||skinAwardOverlay.style.display!=DisplayStyle.Flex)return;
            bool rare=awardGrade>=3;float age=Mathf.Max(0,Time.unscaledTime-awardStarted);Color color=awardGrade==4?new Color(1,.16f,.10f):new Color(1,.76f,.22f);
            Show(awardHalo,rare);Show(awardFlash,rare&&age<.45f);
            float pulse=1+.05f*Mathf.Sin(age*(awardGrade==4?8:4));awardHalo.style.scale=new Scale(new Vector3(pulse,pulse,1));
            awardHalo.style.borderTopColor=color;awardHalo.style.borderBottomColor=color;awardHalo.style.borderLeftColor=color;awardHalo.style.borderRightColor=color;
            awardHalo.style.opacity=rare?Mathf.Lerp(.35f,.85f,Mathf.Exp(-age*2)):.0f;
            awardFlash.style.backgroundColor=new Color(color.r,color.g,color.b,.5f*Mathf.Clamp01(1-age/.45f));
            awardCard.style.borderTopColor=color;awardCard.style.borderBottomColor=color;awardCard.style.borderLeftColor=color;awardCard.style.borderRightColor=color;
            for(int i=0;i<awardSparks.Length;i++)
            {
                var spark=awardSparks[i];Show(spark,rare);float a=i*Mathf.PI*2/awardSparks.Length+age*(awardGrade==4?.45f:.18f);
                float radius=27+Mathf.Sin(age*3+i)*2;spark.style.left=Length.Percent(50+Mathf.Cos(a)*radius);spark.style.top=Length.Percent(50+Mathf.Sin(a)*radius);
                spark.style.color=color;spark.style.opacity=rare?.45f+.5f*Mathf.Abs(Mathf.Sin(age*4+i)):0;
                spark.style.fontSize=awardGrade==4?26:18;
            }
        }
        private void StartGachaSpin(int count)
        {
            if(gachaSpinning||manager.Busy||count<1||count>10)return;
            int cost=count*10;if(GachaGold<cost){gachaResult.text="골드가 부족합니다.";UpdateGachaGold();return;}
            skipAwardAnimation=false;batchCount=count;batchCreditTotal=0;bool hasRare=false;int best=0;
            for(int i=0;i<count;i++)
            {
                int prize,category,skin;
                do
                {
                    prize=Random.Range(0,PrizeCount);
                    if(prize<DuelLoadout.ItemCount*DuelSkins.Colors){category=prize/DuelSkins.Colors;skin=prize%DuelSkins.Colors+1;}
                    else {prize-=DuelLoadout.ItemCount*DuelSkins.Colors;category=DuelLoadout.ItemCount+prize/DuelSkins.WeaponSkinCount;skin=prize%DuelSkins.WeaponSkinCount+1;}
                }while(count==10&&i==9&&!hasRare&&PrizeGrade(category,skin)<2);
                batchCategories[i]=category;batchSkins[i]=skin;
                batchDuplicates[i]=DuelSkins.AwardPending(category,skin);
                if(batchDuplicates[i])batchCreditTotal+=DuplicateCredit(PrizeGrade(category,skin));
                if(PrizeGrade(category,skin)>=2)hasRare=true;
                if(PrizeGrade(category,skin)>PrizeGrade(batchCategories[best],batchSkins[best]))best=i;
            }
            PlayerPrefs.SetInt(GoldKey,GachaGold-cost);PlayerPrefs.SetInt(CreditKey,(int)System.Math.Min(int.MaxValue,(long)GachaCredits+batchCreditTotal));PlayerPrefs.Save();
            prizeItem=batchCategories[best];prizeColor=batchSkins[best];UpdateGachaGold();
            int grade=PrizeGrade(prizeItem,prizeColor),matches=0;
            for(int i=0;i<20;i++)if(DuelSkins.Grade(i%10+1)==grade)matches++;
            int pick=Random.Range(0,matches);spinSegment=0;
            for(int i=0;i<20;i++)if(DuelSkins.Grade(i%10+1)==grade&&pick--==0){spinSegment=i;break;}
            gachaWheel3D.PrepareSpin();awaitingGachaBall=true;
            gachaTick=Mathf.FloorToInt(wheelAngle/12);gachaAudio?.Tick();gachaSpinning=true;Show(gachaControls,false);Show(gachaSkip,true);gachaResult.text="룰렛을 클릭해 공을 던지세요.";
        }
        private void ThrowGachaBall()
        {
            if(!gachaSpinning||!awaitingGachaBall)return;
            awaitingGachaBall=false;gachaAudio?.ResetBall();gachaWheel3D.BeginBall(spinSegment);spinStart=Time.unscaledTime;spinFrom=wheelAngle;
            float final=360-(spinSegment+.5f)*18;spinTo=wheelAngle+1800+Mathf.Repeat(final-Mathf.Repeat(wheelAngle,360),360);gachaResult.text="룰렛이 돌아갑니다…";
        }
        private void FinishGachaSpin()
        {
            if(!gachaSpinning)return;if(awaitingGachaBall)ThrowGachaBall();gachaSpinning=false;gachaAudio?.ResetBall();SetGachaWheelAngle(spinTo);gachaWheel3D.LandBall();
            int grade=PrizeGrade(prizeItem,prizeColor);bool duplicate=false;
            for(int i=0;i<batchCount;i++)if(batchCategories[i]==prizeItem&&batchSkins[i]==prizeColor){duplicate=batchDuplicates[i];break;}
            gachaResult.text=batchCount>1?batchCount+"회 뽑기 완료"+(batchCreditTotal>0?" · 크레딧 +"+batchCreditTotal:""):DuelSkins.GradeName(grade)+"  ·  "+PrizeName(prizeItem,prizeColor)+(duplicate?"  (중복 · 크레딧 +"+DuplicateCredit(grade)+")":"  획득!");
            gachaResult.style.color=DuelSkins.GradeColor(grade);gachaAudio?.Award(grade);ShowSkinAward(prizeItem,prizeColor,duplicate);
            batchResults.Clear();Show(batchResults,batchCount>1);skinAwardImage.EnableInClassList("batch-preview",batchCount>1);
            if(batchCount>1)
            {
                skinAwardName.text=batchCount+"회 뽑기 결과"+(batchCreditTotal>0?" · 크레딧 +"+batchCreditTotal:"");
                for(int i=0;i<batchCount;i++)
                {
                    int category=batchCategories[i],skin=batchSkins[i],itemGrade=PrizeGrade(category,skin);
                    string label=DuelSkins.GradeName(itemGrade)+"  ·  "+PrizeName(category,skin)+(batchDuplicates[i]?"  (중복 · +"+DuplicateCredit(itemGrade)+")":"");
                    var button=Button(batchResults,label,()=>{ShowPrizeModel(category,skin);skinAwardImage.image=skinModelPreview.Texture;},"gacha-result-entry");
                    button.style.color=DuelSkins.GradeColor(itemGrade);
                    button.RegisterCallback<ClickEvent>(e=>e.StopPropagation());
                }
            }
            Show(gachaControls,true);Show(gachaDetails,true);Show(gachaSkip,false);UpdateGachaGold();
        }
        private void UpdateGacha()
        {
            if(!gachaOpen)return;if(manager.Busy){SetGachaOpen(false);return;}
            if(gachaSpinning){float t=awaitingGachaBall?0:Mathf.Clamp01((Time.unscaledTime-spinStart)/5f);if(awaitingGachaBall)SetGachaWheelAngle(wheelAngle+Time.unscaledDeltaTime*480f);else{SetGachaWheelAngle(Mathf.Lerp(spinFrom,spinTo,1-Mathf.Pow(1-t,3)));gachaWheel3D.AnimateBall(t,wheelAngle);gachaAudio?.UpdateBall(t);}int tick=Mathf.FloorToInt(wheelAngle/12);if(tick!=gachaTick){gachaTick=tick;gachaAudio?.Tick();}if(!awaitingGachaBall&&t>=1)FinishGachaSpin();}
            if(!gachaSpinning&&!inventoryOpen&&skinAwardOverlay.style.display!=DisplayStyle.Flex)SetGachaWheelAngle(wheelAngle+Time.unscaledDeltaTime*6f);
            UpdateAwardAnimation();
            if(UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame){if(creditExchangeLayer.style.display==DisplayStyle.Flex)SetCreditExchange(false);else if(skinAwardOverlay.style.display==DisplayStyle.Flex)CloseSkinPreview();else SetGachaOpen(false);}
        }
        private void DestroyGacha(){if(creditIcon!=null)Destroy(creditIcon);foreach(var preview in skinPreviews)if(preview!=null)Destroy(preview);foreach(var preview in weaponSkinPreviews)if(preview!=null)Destroy(preview);}
    }
}
