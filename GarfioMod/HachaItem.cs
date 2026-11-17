using UnityEngine;

namespace GarfioMod
{
    // Lo que llevas en la mano (el hacha o el arpón). Con la tecla de usar (R) lo lanzas;
    // R otra vez suelta la cuerda y vuelve a tu mano.
    [DefaultExecutionOrder(1000)]
    public abstract class GarfioItem : ShipItem
    {
        // Los ajustes de cada objeto (hacha o arpón)
        public abstract Arma Datos { get; }

        private bool pulsadoEsteFotograma, pulsadoAntes;
        private bool botonLiberado = true;
        private Renderer[] dibujos;
        private Component colgable;   // componente de HooksHangMore, si está instalado

        public override void OnBuy()
        {
            // Al comprarlo la tecla de usar sigue pulsada: esperamos a que se suelte
            botonLiberado = false;
        }

        public override void OnAltHeld()
        {
            pulsadoEsteFotograma = true;
        }

        public override void ExtraLateUpdate()
        {
            Arma a = Datos;
            bool pulsa = pulsadoEsteFotograma;
            pulsadoEsteFotograma = false;
            bool recien = pulsa && !pulsadoAntes;
            pulsadoAntes = pulsa;
            if (!pulsa) botonLiberado = true;
            if (a == null) return;

            // Si HooksHangMore está instalado, la posición en el gancho se actualiza con los ajustes (F1)
            if (Plugin.TipoAttachable != null && Plugin.PropPosColgado != null && Plugin.PropGiroColgado != null)
            {
                if (colgable == null) colgable = GetComponent(Plugin.TipoAttachable);
                if (colgable != null)
                {
                    Plugin.PropPosColgado.SetValue(colgable, new Vector3(a.ColgX.Value, a.ColgY.Value, a.ColgZ.Value), null);
                    Plugin.PropGiroColgado.SetValue(colgable, new Vector3(a.ColgRotX.Value, a.ColgRotY.Value, a.ColgRotZ.Value), null);
                }
            }

            holdDistance = a.ManoDistancia.Value;
            holdHeight = a.ManoAltura.Value;
            if (held == null) return;

            Vector3 g = new Vector3(a.GiroX.Value, a.GiroY.Value, a.GiroZ.Value);
            if (g.sqrMagnitude > 0.0001f) transform.Rotate(g, Space.Self);

            // Desplazamiento lateral (por ejemplo, el arpón sobre el hombro)
            Camera cam = Camera.main;
            if (cam != null && Mathf.Abs(a.ManoLado.Value) > 0.001f) transform.position += cam.transform.right * a.ManoLado.Value;

            if (recien && sold && botonLiberado && Plugin.Instancia != null)
            {
                Plugin.Instancia.PulsarUsar(this);
            }
        }

        // Mientras vuela, el de tu mano no se ve. No apagamos el dibujo (el juego suelta lo
        // que llevas en la mano si deja de "verse"): le ponemos un material totalmente transparente
        private Material[][] originales;
        private static Material invisible;

        public void Esconder(bool si)
        {
            if (dibujos == null) dibujos = GetComponentsInChildren<Renderer>();
            if (invisible == null)
            {
                invisible = new Material(Shader.Find("Sprites/Default"));
                invisible.color = new Color(0f, 0f, 0f, 0f);
            }
            if (si)
            {
                if (originales != null) return;   // ya está escondido
                originales = new Material[dibujos.Length][];
                for (int i = 0; i < dibujos.Length; i++)
                {
                    if (dibujos[i] == null) continue;
                    originales[i] = dibujos[i].sharedMaterials;
                    Material[] nuevos = new Material[originales[i].Length];
                    for (int k = 0; k < nuevos.Length; k++) nuevos[k] = invisible;
                    dibujos[i].sharedMaterials = nuevos;
                }
            }
            else
            {
                if (originales == null) return;
                for (int i = 0; i < dibujos.Length; i++)
                    if (dibujos[i] != null && originales[i] != null) dibujos[i].sharedMaterials = originales[i];
                originales = null;
            }
        }
    }

    public class HachaItem : GarfioItem
    {
        public override Arma Datos { get { return Plugin.Hacha; } }
    }

    public class ArponItem : GarfioItem
    {
        public override Arma Datos { get { return Plugin.Arpon; } }
    }

    public class MikhtafItem : GarfioItem
    {
        public override Arma Datos { get { return Plugin.Mikhtaf; } }
    }

    public class KaginawaItem : GarfioItem
    {
        public override Arma Datos { get { return Plugin.Kaginawa; } }
    }
}
