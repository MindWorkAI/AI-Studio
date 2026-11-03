namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeMedicine(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.PATIENT_CASE => Medicine(TB("Layout for patient cases"), TB("Which slide layout should the presentation of a patient case use?"), "the presentation of a patient case, without anything which identifies the patient"),
        LayoutPurpose.MEDICAL_HISTORY => Medicine(TB("Layout for the medical history"), TB("Which slide layout should the medical history of a patient use?"), "the medical history of a patient"),
        LayoutPurpose.CLINICAL_FINDINGS => Medicine(TB("Layout for clinical findings"), TB("Which slide layout should clinical findings use, e.g., of a physical examination?"), "clinical findings, e.g., of a physical examination"),
        LayoutPurpose.IMAGING => Medicine(TB("Layout for imaging"), TB("Which slide layout should imaging use, e.g., an X-ray, CT, or MRI with its findings?"), "imaging, e.g., an X-ray, CT, or MRI with its findings"),
        LayoutPurpose.LAB_VALUES => Medicine(TB("Layout for lab values"), TB("Which slide layout should lab values use, e.g., with their reference ranges?"), "lab values, e.g., with their reference ranges"),
        LayoutPurpose.DIAGNOSIS => Medicine(TB("Layout for the diagnosis"), TB("Which slide layout should the diagnosis use?"), "the diagnosis"),
        LayoutPurpose.DIFFERENTIAL_DIAGNOSES => Medicine(TB("Layout for differential diagnoses"), TB("Which slide layout should differential diagnoses use?"), "the differential diagnoses, i.e., what else the findings could point to"),
        LayoutPurpose.THERAPY => Medicine(TB("Layout for the therapy"), TB("Which slide layout should the therapy or the treatment plan use?"), "the therapy or the treatment plan"),
        LayoutPurpose.MEDICATION => Medicine(TB("Layout for medication"), TB("Which slide layout should medication use, e.g., with agent, dose, and duration?"), "medication, e.g., with agent, dose, and duration"),
        LayoutPurpose.COURSE_OF_DISEASE => Medicine(TB("Layout for the course of a disease"), TB("Which slide layout should the course of a disease use?"), "the course of a disease or of its treatment"),
        LayoutPurpose.PROGNOSIS => Medicine(TB("Layout for the prognosis"), TB("Which slide layout should the prognosis use?"), "the prognosis"),
        LayoutPurpose.ADVERSE_EVENTS => Medicine(TB("Layout for adverse events"), TB("Which slide layout should side effects and adverse events use?"), "side effects and adverse events"),
        LayoutPurpose.GUIDELINE_RECOMMENDATION => Medicine(TB("Layout for guideline recommendations"), TB("Which slide layout should a recommendation of a guideline use, e.g., with its level of evidence?"), "a recommendation of a clinical guideline, e.g., with its level of evidence and its grade of recommendation"),
        LayoutPurpose.STUDY_POPULATION => Medicine(TB("Layout for the study population"), TB("Which slide layout should the study population use, e.g., with inclusion and exclusion criteria?"), "the study population, e.g., with inclusion and exclusion criteria or a flow diagram"),
        LayoutPurpose.ENDPOINTS => Medicine(TB("Layout for endpoints"), TB("Which slide layout should the endpoints of a study use?"), "the endpoints of a study, e.g., the primary and the secondary ones"),
        LayoutPurpose.ETHICS_APPROVAL => Medicine(TB("Layout for the ethics approval"), TB("Which slide layout should the approval of an ethics committee use?"), "the approval of an ethics committee"),

        _ => null,
    };

    private static LayoutPurposeInfo Medicine(string label, string question, string promptDescription) => new(LayoutPurposeArea.MEDICINE, label, question, promptDescription);
}