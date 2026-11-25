using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CampusMonday {
 public sealed class MondayDashboard:MonoBehaviour {
  FullCampus campus; MondaySimulation sim; UIDocument document; PanelSettings panel;
  VisualElement root,overview,explore,simulationTab,spacesTab;
  Label clock,total,students,teachers,admins,status,playState,playerClock,playerPrompt,scenarioNote;
  Button pause,baseScenario,alternative,simTabButton,spacesTabButton,roomButton; Button[] floorButtons;
  Slider time; DropdownField speed; float nextRefresh; bool initialized;
  bool interfaceVisible=true; Button restoreInterface; int visibilityChangedFrame=-1;
  readonly List<string> speeds=new List<string>{"Tiempo real (1:1)","0,25 min/s","0,5 min/s","1 min/s","2 min/s","5 min/s","10 min/s","20 min/s"};
  readonly float[] speedValues={1f/60f,.25f,.5f,1,2,5,10,20};
  public bool HasKeyboardFocus=>interfaceVisible&&root?.focusController?.focusedElement!=null;
  public bool PointerOverControls {
   get {if(Time.frameCount==visibilityChangedFrame)return true;if(root?.panel==null||Mouse.current==null)return false;var p=Mouse.current.position.ReadValue();p.y=Screen.height-p.y;var picked=root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel,p));return picked!=null&&picked!=root&&picked!=overview&&picked!=explore;}
  }
  static VisualElement Box(VisualElement parent,string classes){var v=new VisualElement();foreach(var c in classes.Split(' '))v.AddToClassList(c);parent.Add(v);return v;}
  static Label Text(VisualElement parent,string text,string classes=""){var l=new Label(text);foreach(var c in classes.Split(' '))if(c.Length>0)l.AddToClassList(c);parent.Add(l);return l;}
  static Button Action(VisualElement parent,string text,Action action,string classes=""){var b=new Button(action){text=text};foreach(var c in classes.Split(' '))if(c.Length>0)b.AddToClassList(c);parent.Add(b);return b;}
  public void Initialize(MondaySimulation simulation){
   sim=simulation;InitializePanel();
   BuildHeader();BuildSidebar();BuildTimeline();BuildExplore();BuildVisibility();Refresh();
  }
  void InitializePanel(){
   panel=ScriptableObject.CreateInstance<PanelSettings>();panel.name="Campus · interfaz nítida";panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.sortingOrder=100;
#if UNITY_EDITOR
   panel.scale=Mathf.Max(1,UnityEditor.EditorGUIUtility.pixelsPerPoint);
#else
   panel.scale=Mathf.Clamp(Screen.dpi/96f,1,2);
#endif
   panel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("CampusTheme");
   var host=new GameObject("Interfaz del campus",typeof(UIDocument));host.transform.SetParent(transform,false);document=host.GetComponent<UIDocument>();document.panelSettings=panel;
   root=document.rootVisualElement;root.name="campus-ui";root.pickingMode=PickingMode.Ignore;root.styleSheets.Add(Resources.Load<StyleSheet>("CampusDashboard"));
   overview=Box(root,"overview");overview.pickingMode=PickingMode.Ignore;
  }
  void BuildVisibility(){
   restoreInterface=Action(root,"Mostrar interfaz · F1",()=>SetInterfaceVisible(true),"restore-interface");
   initialized=true;
  }
  public void Initialize(FullCampus simulation){
   campus=simulation;InitializePanel();BuildHeader();
   var sidebar=Box(overview,"sidebar surface");
   var heading=Box(sidebar,"panel-heading");
   Text(heading,"Centro de control","section-title");playState=Text(heading,"LISTO","live-state");
   var stats=Box(sidebar,"stats");
   foreach(var item in new[]{("Estudiante","Estudiantes","student"),("Profesor","Profesores","teacher"),("Administrativo","Administrativos","admin")})
    Stat(stats,item.Item2,item.Item3).text=campus.Capacity(item.Item1).ToString("N0");
   var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("panel-scroll");
   scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;sidebar.Add(scroll);
   var controls=Box(scroll.contentContainer,"tab-content");
   Text(controls,"VELOCIDAD DEL HORARIO","eyebrow field-heading");
   speed=new DropdownField(speeds,0);speed.AddToClassList("speed");
   speed.RegisterValueChangedCallback(e=>campus.SetRate(speedValues[speeds.IndexOf(e.newValue)]*60));
   controls.Add(speed);
   Text(controls,"VISTA POR PISO","eyebrow field-heading");
   var floors=new List<string>{"Todos"};
   for(int f=1;f<=campus.MaxFloor;f++)floors.Add("Hasta piso "+f);
   var floor=new DropdownField(floors,0);floor.AddToClassList("speed");
   floor.tooltip="Corte por altura aproximada. Los niveles varían entre edificios.";
   floor.RegisterValueChangedCallback(e=>campus.SetFloor(floors.IndexOf(e.newValue)));controls.Add(floor);
   Text(controls,"PRUEBA POR EDIFICIO","eyebrow field-heading");
   var buildings=new DropdownField(campus.BuildingTests(),0);
   buildings.AddToClassList("speed");
   controls.Add(buildings);
   Action(controls,"Probar edificio seleccionado",()=>campus.TestOneBuilding(buildings.value),"primary full");
   Action(controls,"Reiniciar lunes",campus.Restart,"secondary full");
   Action(controls,"Explorar en tercera persona · TAB",()=>campus.SetPlayer(true),"primary full");
   Action(controls,"Exportar CSV",campus.Export,"quiet");
   Action(controls,"Abrir resultados",campus.OpenResults,"quiet");
   var timeline=Box(overview,"timeline surface");
   var bar=Box(timeline,"row timeline-heading");
   pause=Action(bar,"Iniciar lunes",campus.ToggleRun,"pause");status=Text(bar,"","status muted");
   Text(overview,"Izquierdo arrastra · Rueda acerca · P pausa","camera-hint");
   explore=Box(root,"explore");explore.pickingMode=PickingMode.Ignore;
   var card=Box(explore,"explore-card surface");playerClock=Text(card,"","brand-title");
   Action(card,"Volver a simulación · TAB",()=>campus.SetPlayer(false),"quiet");
   var help=Box(explore,"explore-help surface");
   Text(help,"WASD mover · Shift correr · Espacio saltar · E sentarse","help-line");
   playerPrompt=Text(help,"","interaction");
   BuildVisibility();RefreshCampus();
  }
  void RefreshCampus(){
   ApplyVisibility();
   root.EnableInClassList("compact",Screen.width/panel.scale<1100);
   root.EnableInClassList("short",Screen.height/panel.scale<760);
   clock.text=campus.Clock;playerClock.text="Lunes · "+clock.text;
   playerPrompt.text=campus.PlayerPrompt;
   status.text=campus.Status;status.tooltip=campus.Status;
   pause.text=campus.Running?(campus.Paused?"Continuar":"Pausar"):"Iniciar lunes";
   playState.text=campus.Running?(campus.Paused?"EN PAUSA":"EN CURSO"):(campus.Started?"FINALIZADO":"LISTO");
  }
  void BuildHeader(){
   var header=Box(overview,"header surface");var brand=Box(header,"row brand");Text(brand,"U","monogram");var names=Box(brand,"brand-text");Text(names,"Campus / Lunes","brand-title");Text(names,"UNIGUAJIRA  ·  SIMULACIÓN ACADÉMICA","eyebrow");
   var right=Box(header,"row header-right");Text(right,"SEMANA 14","badge");Action(right,"Ocultar · F1",()=>SetInterfaceVisible(false),"visibility-button");var timeBox=Box(right,"header-clock");clock=Text(timeBox,"06:28","clock");Text(timeBox,"LUNES","eyebrow");
  }
  void BuildSidebar(){
   var sidebar=Box(overview,"sidebar surface");var heading=Box(sidebar,"panel-heading");Text(heading,"Centro de control","section-title");playState=Text(heading,"EN CURSO","live-state");
   var stats=Box(sidebar,"stats");var number=Box(stats,"row total-row");total=Text(number,"0","total");Text(number,"personas\npresentes","muted total-caption");
   students=Stat(stats,"Estudiantes","student");teachers=Stat(stats,"Profesores","teacher");admins=Stat(stats,"Administrativos","admin");
   var tabs=Box(sidebar,"row tabs");simTabButton=Action(tabs,"Simulación",()=>SelectTab(false),"tab selected");spacesTabButton=Action(tabs,"Espacios",()=>SelectTab(true),"tab");
   var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("panel-scroll");scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;sidebar.Add(scroll);
   simulationTab=Box(scroll.contentContainer,"tab-content");Text(simulationTab,"ESCENARIO","eyebrow");var choices=Box(simulationTab,"row segmented");baseScenario=Action(choices,"Base",()=>SetScenario(false),"segment");alternative=Action(choices,"Alternativo",()=>SetScenario(true),"segment");scenarioNote=Text(simulationTab,"","muted description");
   Text(simulationTab,"VELOCIDAD DEL HORARIO","eyebrow field-heading");speed=new DropdownField(speeds,2);speed.AddToClassList("speed");speed.tooltip="Tiempo real (1:1): un segundo simulado por segundo real. Las demás opciones indican minutos simulados por segundo real.";speed.RegisterValueChangedCallback(e=>{int i=speeds.IndexOf(e.newValue);if(i>=0)sim.minutesPerSecond=speedValues[i];});simulationTab.Add(speed);
   Action(simulationTab,"Reiniciar lunes",()=>{sim.Restart();Refresh();},"secondary full");
   spacesTab=Box(scroll.contentContainer,"tab-content");Text(spacesTab,"VISTA POR PISO","eyebrow");var floors=Box(spacesTab,"row segmented");floorButtons=new Button[4];for(int i=0;i<4;i++){int f=i;floorButtons[i]=Action(floors,i==0?"Todos":i.ToString(),()=>{sim.floor=f;Refresh();},"segment");}
   Text(spacesTab,"AULA","eyebrow field-heading");var roomPicker=Box(spacesTab,"row room-picker");Action(roomPicker,"‹",()=>CycleRoom(-1),"room-arrow");roomButton=Action(roomPicker,sim.focusRoom,()=>sim.Focus(sim.focusRoom,true),"room-button");
   Action(roomPicker,"›",()=>CycleRoom(1),"room-arrow");Text(spacesTab,"Selecciona el aula para acercar la cámara.","muted description");
   Action(spacesTab,"Vista general del bloque",()=>sim.Focus(sim.focusRoom,false),"secondary full");Action(spacesTab,"Ver oficinas",()=>sim.ViewOffices(),"secondary full");
   var footer=Box(sidebar,"panel-footer");Text(footer,"Datos del grupo · alcance en informe","footer-note");Action(footer,"Explorar en tercera persona  ·  TAB",()=>{if(sim.player)sim.player.SetControlled(true);},"primary full");var exports=Box(footer,"row export-row");Action(exports,"Exportar CSV",()=>sim.Export(),"quiet");Action(exports,"Captura PNG",()=>sim.Capture(),"quiet");
   SelectTab(false);
  }
  Label Stat(VisualElement parent,string title,string color){var row=Box(parent,"row stat-row");Text(row,"","dot "+color);Text(row,title,"stat-name");return Text(row,"0","stat-value");}
  void BuildTimeline(){
   var context=Box(overview,"world-caption surface");Text(context,"ESCENARIO DEL LUNES","eyebrow");Text(context,"3.008 visitas  /  94 clases","context-value");Text(context,"12 administrativos","muted");
   var timeline=Box(overview,"timeline surface");var top=Box(timeline,"row timeline-heading");pause=Action(top,"Pausar",()=>{sim.paused=!sim.paused;Refresh();},"pause");Text(top,"Línea de tiempo","timeline-title");status=Text(top,"","status muted");
   time=new Slider(388,1380);time.name="day-timeline";time.AddToClassList("time-slider");time.tooltip="Desplazar la hora del lunes";time.RegisterValueChangedCallback(e=>sim.Seek(e.newValue));timeline.Add(time);
   var ticks=Box(timeline,"row ticks");foreach(var tick in new[]{"06:28","10:00","14:00","18:00","23:00"})Text(ticks,tick,"muted");
   Text(overview,"Arrastrar: izquierdo mueve · derecho rota   |   Rueda: zoom","camera-hint");
  }
  void BuildExplore(){
   explore=Box(root,"explore");explore.pickingMode=PickingMode.Ignore;var card=Box(explore,"explore-card surface");Text(card,"EXPLORAR EL CAMPUS","eyebrow");playerClock=Text(card,"Lunes · 06:28","brand-title");Action(card,"Volver a la simulación  ·  TAB",()=>sim.player.SetControlled(false),"quiet");Action(card,"Ocultar interfaz · F1",()=>SetInterfaceVisible(false),"quiet");
   Text(explore,"+","crosshair").pickingMode=PickingMode.Ignore;
   var help=Box(explore,"explore-help surface");Text(help,"WASD  Moverse     Shift  Correr     Espacio  Saltar","help-line");Text(help,"E  Sentarse / levantarse     P  Pausar horario     Esc  Liberar ratón","muted");playerPrompt=Text(help,"","interaction");
  }
  void SelectTab(bool spaces){simulationTab.style.display=spaces?DisplayStyle.None:DisplayStyle.Flex;spacesTab.style.display=spaces?DisplayStyle.Flex:DisplayStyle.None;simTabButton.EnableInClassList("selected",!spaces);spacesTabButton.EnableInClassList("selected",spaces);}
  void CycleRoom(int direction){int index=Array.FindIndex(sim.rooms,r=>r.id==sim.focusRoom);sim.Focus(sim.rooms[(index+direction+sim.rooms.Length)%sim.rooms.Length].id,true);Refresh();}
  void SetScenario(bool alt){if(sim.settings.alternative==alt)return;sim.settings.alternative=alt;sim.Restart();Refresh();}
  void SetInterfaceVisible(bool visible){
   interfaceVisible=visible;visibilityChangedFrame=Time.frameCount;
   if(root.focusController?.focusedElement is VisualElement focused)focused.Blur();
   ApplyVisibility();
  }
  void ApplyVisibility(){bool playing=campus?campus.PlayerControlled:sim.player&&sim.player.controlled;overview.style.display=interfaceVisible&&!playing?DisplayStyle.Flex:DisplayStyle.None;explore.style.display=interfaceVisible&&playing?DisplayStyle.Flex:DisplayStyle.None;restoreInterface.style.display=interfaceVisible?DisplayStyle.None:DisplayStyle.Flex;}
  void Update(){if(!initialized)return;var keyboard=Keyboard.current;if(keyboard!=null&&keyboard.f1Key.wasPressedThisFrame)SetInterfaceVisible(!interfaceVisible);if(!campus&&sim.engine==null)return;if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.1f;if(campus)RefreshCampus();else Refresh();}}
  void Refresh(){if(sim.engine==null)return;ApplyVisibility();root.EnableInClassList("compact",Screen.width/panel.scale<1100);root.EnableInClassList("short",Screen.height/panel.scale<760);roomButton.text="Aula "+sim.focusRoom;
   string hour=$"{(int)sim.clock/60:00}:{(int)sim.clock%60:00}:{(int)(sim.clock*60)%60:00}";clock.text=hour;playerClock.text="Lunes · "+hour;playerPrompt.text=sim.player?sim.player.InteractionPrompt:"";
   total.text=sim.engine.Count(sim.clock).ToString("N0");students.text=sim.engine.Count(sim.clock,Role.Estudiante).ToString("N0");teachers.text=sim.engine.Count(sim.clock,Role.Profesor).ToString();admins.text=sim.engine.Count(sim.clock,Role.Administrativo).ToString();
   pause.text=sim.paused?"Continuar":"Pausar";playState.text=sim.paused?"EN PAUSA":"EN CURSO";playState.EnableInClassList("paused",sim.paused);time.SetValueWithoutNotify((float)sim.clock);status.text=sim.StatusMessage;status.tooltip=sim.StatusMessage;
   baseScenario.EnableInClassList("selected",!sim.settings.alternative);alternative.EnableInClassList("selected",sim.settings.alternative);scenarioNote.text=sim.settings.alternative?"Acceso principal 60 %  ·  Trasero 40 %":"Acceso principal 90 %  ·  Trasero 10 %";
   for(int i=0;i<4;i++)floorButtons[i].EnableInClassList("selected",sim.floor==i);
   int closest=0;for(int i=1;i<speedValues.Length;i++)if(Mathf.Abs(speedValues[i]-sim.minutesPerSecond)<Mathf.Abs(speedValues[closest]-sim.minutesPerSecond))closest=i;speed.SetValueWithoutNotify(speeds[closest]);
  }
  void OnDestroy(){if(panel)Destroy(panel);}
 }
}


