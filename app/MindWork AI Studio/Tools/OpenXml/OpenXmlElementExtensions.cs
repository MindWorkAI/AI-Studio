using DocumentFormat.OpenXml;

namespace AIStudio.Tools.OpenXml;

internal static class OpenXmlElementExtensions
{
    /// <param name="element">The element.</param>
    /// <typeparam name="T">The type of the element.</typeparam>
    extension<T>(T element) where T : OpenXmlElement
    {
        /// <summary>
        /// Takes the element out of its parent, if it has one, so that it can move to another place.
        /// </summary>
        /// <remarks>
        /// Moving keeps everything within the element, e.g., a text box with the new words of its own paragraphs,
        /// which a copy made before those were written would lack.
        /// </remarks>
        /// <returns>The element, without a parent.</returns>
        public T Detached()
        {
            if (element.Parent is not null)
                element.Remove();

            return element;
        }
    }
}