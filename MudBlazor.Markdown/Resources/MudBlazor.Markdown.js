import hljs from "highlight.js";

const codeStylesDir = "code-styles";
const codeStylesSegment = `/MudBlazor.Markdown/${codeStylesDir}/`;

window.highlightCodeElement = function (element) {
	hljs.highlightElement(element);
}

window.setHighlightStylesheet = function (stylesheetPath) {
	let isFound = false;
	const stylesheets = document.querySelectorAll("link[rel='stylesheet']");

	for (let i = 0; i < stylesheets.length; i++) {
		const href = stylesheets[i].getAttribute("href");

		if (!href) {
			continue;
		}

		const index = href.indexOf(codeStylesSegment);

		if (index !== -1) {
			if (!isFound) {
				isFound = true;
				const newHref = href.substring(0, index + codeStylesSegment.length) + stylesheetPath;
				stylesheets[i].setAttribute("href", newHref);
			} else {
				stylesheets[i].remove();
			}
		}
	}

	if (!isFound) {
		const link = document.createElement("link");
		link.rel = "stylesheet";
		link.href = `_content/MudBlazor.Markdown/${codeStylesDir}/${stylesheetPath}`;

		document.head.appendChild(link);
	}
}

window.scrollToElementId = function (elementId) {
	const element = document.getElementById(elementId);
	if (element) {
		const elementIdHref = `#${elementId}`;
		if (!window.location.pathname.endsWith(elementIdHref)) {
			history.replaceState(null, "", window.location.pathname + elementIdHref);
		}

		element.scrollIntoView({
			behavior: "smooth",
			block: "start",
			inline: "nearest"
		});
	}
}

window.appendMathJaxScript = function (scriptId) {
	if (document.getElementById(scriptId)) {
		return;
	}

	const script = document.createElement("script");
	script.id = scriptId;
	script.type = "text/javascript";
	script.src = "_content/MudBlazor.Markdown/MudBlazor.Markdown.MathJax.min.js";

	document.head.appendChild(script);
}

window.refreshMathJaxScript = function () {
	try {
		MathJax.typeset();
	} catch (e) {
		// swallow since in some cases MathJax might not be initialized
	}
}

window.copyTextToClipboard = async function (text) {
	try {
		await navigator.clipboard.writeText(text);
		return true;
	} catch (e) {
		return false;
	}
}
