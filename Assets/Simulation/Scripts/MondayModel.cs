using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CampusMonday {
 [Serializable] public class Lesson { public string room; public int start,end,students,sourceRow; }
 [Serializable] public class MondayData { public string day; public int studentsPerRoom,administrators,adminStart,adminEnd; public float mainShare; public Lesson[] classes; }
 [Serializable] public class ModelSettings {
   public int seed=1401; public float speedMin=1.1f,speedMax=1.5f,arrivalWindow=5,doorSeconds=1.2f,mainShare=.9f;
   public int capacity=32; public bool alternative;
 }
 [Serializable] public class RoomDefinition {
   public string id,modelPrefix; public int floor; public Vector3 board,teacher,door,corridor; public Vector3[] seats;
 }
 public enum Role { Estudiante, Profesor, Administrativo }
 public enum Phase { Fuera, Llegando, Esperando, Entrando, Sentado, EnTablero, Trabajando, Saliendo, Finalizado }
 public class PersonPlan {
   public int id,lesson,seat; public Role role; public string room; public bool main;
   public double arrival,doorArrival,admitted,seated,release,exitStart,depart,speed;
   public Vector3[] approach,inward,outward; public float approachLength,inLength,outLength;
   public Phase State(double t) {
     if(t<arrival)return Phase.Fuera; if(t<doorArrival)return Phase.Llegando; if(t<admitted)return Phase.Esperando;
     if(t<seated)return Phase.Entrando; if(t<release)return role==Role.Estudiante?Phase.Sentado:role==Role.Profesor?Phase.EnTablero:Phase.Trabajando;
     if(t<exitStart)return Phase.Esperando; if(t<depart)return Phase.Saliendo; return Phase.Finalizado;
   }
   public static float Length(Vector3[] p) {float v=0;for(int i=1;i<p.Length;i++)v+=Vector3.Distance(p[i-1],p[i]);return v;}
   public static Vector3 At(Vector3[] p,double fraction,out Vector3 facing) {
     facing=Vector3.forward; if(p==null||p.Length==0)return Vector3.zero;
     float d=Length(p)*Mathf.Clamp01((float)fraction);
     for(int i=1;i<p.Length;i++){float l=Vector3.Distance(p[i-1],p[i]);if(d<=l){facing=p[i]-p[i-1];return Vector3.Lerp(p[i-1],p[i],l>0?d/l:1);}d-=l;}
     return p[p.Length-1];
   }
   public Vector3 Position(double t,out Vector3 facing) {
     if(t<doorArrival)return At(approach,(t-arrival)/Math.Max(.00001,doorArrival-arrival),out facing);
     if(t<admitted){facing=inward[1]-inward[0];return inward[0];}
     if(t<seated)return At(inward,(t-admitted)/Math.Max(.00001,seated-admitted),out facing);
     if(t<exitStart){facing=Vector3.forward;return inward[inward.Length-1];}
     return At(outward,(t-exitStart)/Math.Max(.00001,depart-exitStart),out facing);
   }
 }
 public class MondayEngine {
   public readonly List<PersonPlan> people=new List<PersonPlan>(); public int pathFailures; public ModelSettings settings;
   public delegate Vector3[] Router(Vector3 a,Vector3 b);
   public MondayEngine(MondayData data,RoomDefinition[] rooms,Vector3 main,Vector3 rear,Vector3 office,ModelSettings s,Router route,Vector3[] officeSeats=null,Vector3[] officeDoors=null) {
     settings=s;var rng=new System.Random(s.seed);double Rand()=>rng.NextDouble();
     var lookup=rooms.ToDictionary(r=>r.id); int id=0;
     Vector3[] Path(Vector3 a,Vector3 b){var p=route(a,b);if(p==null||p.Length<2){pathFailures++;throw new InvalidOperationException("Ruta no conectada: "+a+" -> "+b);}return p;}
     void Add(Role role,int li,string room,int seat,int start,int end,Vector3 target,Vector3 door) {
       bool m=role==Role.Administrativo||Rand()<s.mainShare;
       double speed=s.speedMin+Rand()*(s.speedMax-s.speedMin);
       var entrance=m?main:rear;
       var p=new PersonPlan{id=id++,role=role,lesson=li,room=room,seat=seat,main=m,speed=speed,
         arrival=start+Rand()*s.arrivalWindow,release=end,approach=Path(entrance,door),inward=Path(door,target),outward=Path(target,entrance)};
       p.approachLength=PersonPlan.Length(p.approach);p.inLength=PersonPlan.Length(p.inward);p.outLength=PersonPlan.Length(p.outward);
       p.doorArrival=p.arrival+p.approachLength/speed/60;people.Add(p);
     }
     for(int li=0;li<data.classes.Length;li++){
       var c=data.classes[li];var r=lookup[c.room];
       if(c.students>s.capacity||r.seats.Length<c.students)throw new InvalidOperationException("Capacidad insuficiente "+c.room);
       for(int k=0;k<c.students;k++)Add(Role.Estudiante,li,c.room,k,c.start,c.end,r.seats[k],r.door);
       Add(Role.Profesor,li,c.room,-1,c.start,c.end,r.teacher,r.door);
     }
     for(int k=0;k<data.administrators;k++)Add(Role.Administrativo,-1,"Oficina_"+(k/2+1),k,data.adminStart,data.adminEnd,officeSeats!=null?officeSeats[k]:office+new Vector3(k%4*.65f,0,k/4*.7f),officeDoors!=null?officeDoors[k]:office+Vector3.back*2);
     // Each door is a shared FIFO server. Departing cohorts release their seats before the next cohort enters.
     foreach(var group in people.GroupBy(p=>p.room)){
       var events=new List<(double time,bool exit,PersonPlan p)>();
       foreach(var p in group){events.Add((p.doorArrival,false,p));events.Add((p.release,true,p));}
       double free=0;var seatFree=new Dictionary<int,double>();
       foreach(var e in events.OrderBy(e=>e.time).ThenBy(e=>e.exit?0:1).ThenBy(e=>e.p.id)){
         var p=e.p; double service=s.doorSeconds/60;
         if(e.exit){p.exitStart=Math.Max(Math.Max(p.release,p.seated),free);free=p.exitStart+service;p.depart=p.exitStart+p.outLength/p.speed/60;seatFree[p.seat]=p.exitStart+service;}
         else {double sf=seatFree.TryGetValue(p.seat,out double v)?v:0;p.admitted=Math.Max(e.time,Math.Max(free,sf));free=p.admitted+service;p.seated=p.admitted+p.inLength/p.speed/60;}
       }
     }
   }
   public int Count(double t,Role? role=null)=>people.Count(p=>p.arrival<=t&&t<p.depart&&(!role.HasValue||p.role==role));
   public void Export(string folder,string tag,MondayData data) {
     Directory.CreateDirectory(folder);var ci=CultureInfo.InvariantCulture;string F(double n)=>n.ToString("0.0000",ci);
     var lines=new List<string>{"id,rol,aula,fila_excel,acceso,llegada_min,admision_min,sentado_min,fin_clase_min,salida_min,espera_s,recorrido_entrada_s,velocidad_m_s,distancia_acceso_m,distancia_interior_m,distancia_salida_m,llegada_puerta_min,inicio_salida_min,espera_salida_s"};
     foreach(var p in people)lines.Add(string.Join(",",p.id,p.role,p.room,p.lesson<0?0:data.classes[p.lesson].sourceRow,p.main?"Principal":"Trasera",F(p.arrival),F(p.admitted),F(p.seated),F(p.release),F(p.depart),F((p.admitted-p.doorArrival)*60),F((p.seated-p.arrival)*60),F(p.speed),F(p.approachLength),F(p.inLength),F(p.outLength),F(p.doorArrival),F(p.exitStart),F((p.exitStart-p.release)*60)));
     File.WriteAllLines(Path.Combine(folder,tag+"_agentes.csv"),lines);
     lines=new List<string>{"inicio_min,fin_min,est_entradas_excel,est_entradas_sim,est_salidas_excel,est_salidas_sim,ocupacion_excel,ocupacion_sim_sin_profesores,profesores_presentes"};
     for(int t=390;t<1380;t+=45){int end=t+45;int ei=data.classes.Where(c=>c.start>=t&&c.start<end).Sum(c=>c.students);int eo=data.classes.Where(c=>c.end>=t&&c.end<end).Sum(c=>c.students);
       int si=people.Count(p=>p.role==Role.Estudiante&&p.arrival>=t&&p.arrival<end),so=people.Count(p=>p.role==Role.Estudiante&&p.depart>=t&&p.depart<end);
       int occ=data.classes.Where(c=>c.start<end&&c.end>=end).Sum(c=>c.students)+(data.adminStart<end&&data.adminEnd>=end?data.administrators:0);
       lines.Add(string.Join(",",t,end,ei,si,eo,so,occ,Count(end-.00001,Role.Estudiante)+Count(end-.00001,Role.Administrativo),Count(end-.00001,Role.Profesor)));}
     File.WriteAllLines(Path.Combine(folder,tag+"_franjas.csv"),lines);
   }
 }
}
// Refresh compilation
