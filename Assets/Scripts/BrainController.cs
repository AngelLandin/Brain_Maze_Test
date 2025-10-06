using UnityEngine;
using System.Collections.Generic; // Para usar Listas
using System.Collections;
using LSL; // Necesario para recibir datos LSL
using TMPro; // Para controlar el texto en pantalla

public class BrainController : MonoBehaviour
{
    // --- Variables de Conexión LSL ---
    private StreamInlet inlet;
    private float[] sample;
    private const int bufferSize = 1;
    private bool streamFound = false;

    // --- Variables de Calibración ---
    [Header("Configuración de Calibración")]
    [SerializeField] private TextMeshProUGUI instructionsText; // Arrastra tu objeto de texto aquí
    [SerializeField] private float restTime = 10f; // Duración de la fase de reposo [cite: 194]
    [SerializeField] private float focusTime = 10f; // Duración de la fase de foco [cite: 195]

    private List<float> calibrationData = new List<float>();
    private float mean_mu = 0f;    // Promedio (μ) [cite: 196]
    private float stdDev_sigma = 1f; // Desviación Estándar (σ) [cite: 196]
    private bool isCalibrated = false;

    // --- Variables de Jugabilidad ---
    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float zScoreThreshold = 1.0f; // Umbral de Z-Score para moverse [cite: 211]

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sample = new float[bufferSize];
        StartCoroutine(CalibrationPhase()); // Inicia la calibración al empezar [cite: 193]
    }

    void Update()
    {
        if (!streamFound)
        {
            // Busca el stream de LSL continuamente hasta encontrarlo
            StreamInfo[] results = LSL.LSL.resolve_stream("name", "Concentration", 1, 0.0);
            if (results.Length > 0)
            {
                inlet = new StreamInlet(results[0]);
                streamFound = true;
                Debug.Log("¡Stream de concentración encontrado y conectado!");
            }
        }
        else if (inlet != null)
        {
            // Una vez encontrado, empieza a recibir datos
            inlet.pull_sample(sample, 0.0f);
            float rawConcentration = sample[0];

            if (isCalibrated)
            {
                // ---- MECÁNICA CENTRAL DEL EEG ----
                [cite_start]// 1. Normalización a Z-Score 
                float zScore = (rawConcentration - mean_mu) / stdDev_sigma;

                [cite_start]// 2. Detección y Acción [cite: 211, 212]
                if (zScore > zScoreThreshold)
                {
                    rb.velocity = transform.up * moveSpeed; // Mover personaje
                }
                else
                {
                    rb.velocity = Vector2.zero; // Detener personaje
                }
            }
            else
            {
                // Si no estamos calibrados, solo recolectamos datos
                calibrationData.Add(rawConcentration);
            }
        }
    }

    IEnumerator CalibrationPhase()
    {
        // --- Fase de Reposo ---
        instructionsText.text = "Calibración: Prepárate para relajarte...";
        yield return new WaitForSeconds(3f);
        instructionsText.text = "¡AHORA! Relájate con los ojos cerrados...";
        yield return new WaitForSeconds(restTime); // Espera durante la fase de reposo [cite: 194]

        // --- Fase de Foco ---
        instructionsText.text = "¡Bien! Ahora, prepárate para concentrarte...";
        yield return new WaitForSeconds(3f);
        instructionsText.text = "¡CONCÉNTRATE! Realiza una tarea mental (ej. contar hacia atrás desde 100)";
        yield return new WaitForSeconds(focusTime); // Espera durante la fase de foco [cite: 195]

        // --- Cálculo de Calibración ---
        instructionsText.text = "Calculando...";
        CalculateCalibration();
        isCalibrated = true;
        yield return new WaitForSeconds(2f);

        // --- Fin ---
        instructionsText.text = "¡Calibración Completa! Usa tu concentración para moverte.";
    }

    void CalculateCalibration()
    {
        if (calibrationData.Count == 0) return;

        // Calcular el promedio (μ)
        float sum = 0;
        foreach (float value in calibrationData)
        {
            sum += value;
        }
        mean_mu = sum / calibrationData.Count;

        // Calcular la desviación estándar (σ)
        float sumOfSquares = 0;
        foreach (float value in calibrationData)
        {
            sumOfSquares += Mathf.Pow(value - mean_mu, 2);
        }
        stdDev_sigma = Mathf.Sqrt(sumOfSquares / calibrationData.Count);

        // Asegurarse de que sigma no sea cero para evitar división por cero
        if (stdDev_sigma == 0) stdDev_sigma = 0.001f;

        Debug.Log($"Calibración finalizada: μ = {mean_mu}, σ = {stdDev_sigma}");
    }
}