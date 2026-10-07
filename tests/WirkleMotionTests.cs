using System;
using QuickPickGraffiti;
internal static class WirkleMotionTests
{
    private static int checks;
    private static void Check(bool okay,string label) { checks++; if(!okay) throw new Exception(label); }
    private static bool Near(float a,float b,float tolerance=.0001f) { return Math.Abs(a-b)<tolerance; }
    public static void Run()
    {
        Check(Near(WirkleMotion.Reveal(0),0),"reveal begins closed");
        Check(Near(WirkleMotion.Reveal(.1f),.5f),"reveal halfway at half duration");
        Check(Near(WirkleMotion.Reveal(.2f),1),"requested .2s completion");
        float last=0;
        for(int i=0;i<=420;i++) { float p=WirkleMotion.Reveal(i/1000f); Check(p>=last && p<=1,"no bounce/overshoot"); last=p; }
        Check(Near(WirkleMotion.Reveal(10),1),"reveal stays open");
        var point=new WirkleMotion.FlowPoint(1.3f,.86f);
        float exact=point.Sample(new WirkleMotion.FlowTime(1f));
        foreach(int fps in new[]{24,30,60,120,144,240})
        {
            for(int frame=0;frame<fps;frame++) point.Sample(new WirkleMotion.FlowTime(frame/(float)fps));
            Check(Near(point.Sample(new WirkleMotion.FlowTime(1f)),exact),"same phase at one second regardless of render rate");
        }
        float a=point.Sample(new WirkleMotion.FlowTime(.001f));
        float b=point.Sample(new WirkleMotion.FlowTime(.002f));
        Check(Math.Abs(a-b)>0.000001f && Math.Abs(a-b)<.001f,"sub-frame motion is continuous, no atlas stepping");
        var seamA=new WirkleMotion.FlowPoint(0,.86f);
        var seamB=new WirkleMotion.FlowPoint((float)(Math.PI*2),.86f);
        Check(Near(seamB.Sample(new WirkleMotion.FlowTime(3))-seamA.Sample(new WirkleMotion.FlowTime(3)),0,.0002f),"liquid wrap is seamless");
        Check(Near(WirkleMotion.SprayFade(0,.1f,2),0),"spray waits for birth");
        Check(WirkleMotion.SprayFade(.3f,.1f,2)>0,"spray fades in");
        Check(WirkleMotion.SprayFade(1.9f,.1f,2)<WirkleMotion.SprayFade(1.2f,.1f,2),"spray fades out");
        Check(WirkleMotion.SprayFade(2.099f,.1f,2)<.003f,"spray disappears at end of life");
        Check(WirkleMotion.SprayFade(2.4f,.1f,2)>0,"spray respawns");
        Check(Near(WirkleMotion.SprayFade(.4f,.1f,2),WirkleMotion.SprayFade(20.4f,.1f,2),.0001f),"spray remains stable over many cycles");
        for(int fpsIndex=0;fpsIndex<6;fpsIndex++)
        {
            int fps=new[]{24,30,60,120,144,240}[fpsIndex];
            for(int frame=1;frame<fps;frame++)
            {
                float t=frame/(float)fps;
                if(t>=.04f) Check(Near(WirkleMotion.SprayFlight(t),1),"speck remains stuck after landing at every frame rate");
            }
        }
        Check(Near(WirkleMotion.SprayFlight(0),0),"speck starts inside rim");
        Check(Near(WirkleMotion.SprayFlight(.02f),.5f),"40ms straight flight");
        Check(Near(WirkleMotion.RevealBand(0,1),0),"rim initially hidden");
        Check(Near(WirkleMotion.RevealBand(.5f,.8f),1),"outer paint revealed first");
        Check(Near(WirkleMotion.RevealBand(.5f,.2f),0),"inner paint stays masked until reached");
        Check(Near(WirkleMotion.RevealBand(1,0),1),"entire band visible by .2 seconds");
        for(int i=0;i<=100;i++)
        {
            float progress=i/100f;
            Check(WirkleMotion.RevealBand(progress,.8f)>=WirkleMotion.RevealBand(progress,.2f),"outer edge always precedes inner edge");
        }
        Console.WriteLine(checks+" reveal, continuous-motion and spray-lifetime checks passed.");
    }
}
