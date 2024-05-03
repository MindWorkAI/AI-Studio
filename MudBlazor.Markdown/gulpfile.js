const { src, dest, series } = require("gulp");
const webpack = require("webpack-stream");
const rename = require("gulp-rename");
const minifyCss = require("gulp-clean-css");
const changeCase = require("change-case");
const all = require("gulp-all");

function fonts() {
	return src("Resources/Fonts/*.woff")
		.pipe(dest("wwwroot/output/chtml/fonts/woff-v2"));
}

function cssMain() {
	return src("Resources/*.css")
		.pipe(minifyCss())
		.pipe(rename({ extname: ".min.css" }))
		.pipe(dest("wwwroot"));
}

function cssCodeStyles() {
	return src("Resources/CodeStyles/src/**/*.css")
		.pipe(minifyCss({ level: { 1: { specialComments: "0" } } }))
		.pipe(rename(function (path) {
			path.dirname = changeCase.camelCase(path.dirname);
			path.extname = ".min.css";
		}))
		.pipe(dest("wwwroot/code-styles"));
}

function img() {
	return src("Resources/CodeStyles/src/**/*.{png,jpg}")
		.pipe(dest("wwwroot/code-styles"));
}

function jsMain() {
	const mainJs = src("Resources/MudBlazor.Markdown.js")
		.pipe(webpack({ mode: "production" }))
		.pipe(rename({ basename: "MudBlazor.Markdown", extname: ".min.js" }))
		.pipe(dest("wwwroot"));

	const mathJax = src("Resources/MudBlazor.Markdown.MathJax.min.js")
		.pipe(dest("wwwroot"));

	return all(mainJs, mathJax);
}

exports.default = series(fonts, cssMain, cssCodeStyles, img, jsMain);
