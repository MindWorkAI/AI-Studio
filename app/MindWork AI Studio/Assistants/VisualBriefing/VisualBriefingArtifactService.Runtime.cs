namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingArtifactService
{
    /// <summary>
    /// Defines the pinned declarative AI Studio briefing runtime.
    /// </summary>
    private const string RUNTIME_SCRIPT = """
                                          (() => {
                                            "use strict";
                                            const VERSION = 1;
                                            const AI_STUDIO_VERSION = "__MWAI_AI_STUDIO_VERSION__";
                                            const dataElement = document.getElementById("mwai-briefing-data");
                                            const root = document.getElementById("mwai-briefing-root");
                                            if (!dataElement || !root) return;
                                            const state = JSON.parse(dataElement.textContent || "{}");
                                            const contexts = new WeakMap();
                                            const get = (path, context = state) => {
                                              if (!path) return undefined;
                                              if (path === "$root") return state;
                                              if (path === ".") return context && Object.hasOwn(context, "$value") ? context.$value : context;
                                              if (path === "$index") return context && context.$index;
                                              if (path === "$value") return context && context.$value;
                                              const isRoot = path.startsWith("$root.");
                                              const normalized = isRoot ? path.slice(6) : path.startsWith(".") ? path.slice(1) : path;
                                              return normalized.split(".").filter(Boolean).reduce((value, key) => value == null ? undefined : value[key], isRoot ? state : path.startsWith(".") ? context : state);
                                            };
                                            const set = (path, value) => {
                                              const parts = (path.startsWith("$root.") ? path.slice(6) : path).split(".").filter(Boolean);
                                              let target = state;
                                              for (let index = 0; index < parts.length - 1; index++) target = target[parts[index]] ??= {};
                                              target[parts.at(-1)] = value;
                                            };
                                            const expression = (node, context) => {
                                              if (node == null || typeof node !== "object") return node;
                                              if ("path" in node) return get(node.path, context);
                                              if ("value" in node) return node.value;
                                              const args = (node.args || []).map(value => expression(value, context));
                                              switch (node.op) {
                                                case "add": return args.reduce((a, b) => a + b, 0);
                                                case "subtract": return args[0] - args[1];
                                                case "multiply": return args.reduce((a, b) => a * b, 1);
                                                case "divide": return args[1] === 0 ? null : args[0] / args[1];
                                                case "power": return Math.pow(args[0], args[1]);
                                                case "eq": return args[0] === args[1];
                                                case "ne": return args[0] !== args[1];
                                                case "gt": return args[0] > args[1];
                                                case "gte": return args[0] >= args[1];
                                                case "lt": return args[0] < args[1];
                                                case "lte": return args[0] <= args[1];
                                                case "if": return args[0] ? args[1] : args[2];
                                                case "min": return Math.min(...args);
                                                case "max": return Math.max(...args);
                                                case "round": return Math.round(args[0] * Math.pow(10, args[1] || 0)) / Math.pow(10, args[1] || 0);
                                                case "sqrt": return Math.sqrt(args[0]);
                                                case "log": return Math.log(args[0]);
                                                case "exp": return Math.exp(args[0]);
                                                default: return null;
                                              }
                                            };
                                            const bind = (container, context = state) => {
                                              container.querySelectorAll("[data-mwai-text]").forEach(element => {
                                                const value = get(element.dataset.mwaiText, contexts.get(element) || context);
                                                element.textContent = value == null ? "" : String(value);
                                              });
                                              container.querySelectorAll("[data-mwai-expr]").forEach(element => {
                                                const localContext = contexts.get(element) || context;
                                                const tree = get(element.dataset.mwaiExpr, localContext);
                                                const value = expression(tree, localContext);
                                                element.textContent = value == null ? "" : String(value);
                                              });
                                              container.querySelectorAll("[data-mwai-if],[data-mwai-filter]").forEach(element => {
                                                const localContext = contexts.get(element) || context;
                                                const conditionValue = element.dataset.mwaiIf ? get(element.dataset.mwaiIf, localContext) : true;
                                                const conditionMatches = Boolean(conditionValue && typeof conditionValue === "object" ? expression(conditionValue, localContext) : conditionValue);
                                                const selected = element.dataset.mwaiFilter ? get(element.dataset.mwaiFilter, localContext) : "";
                                                const filterValue = element.dataset.mwaiFilterValue ? get(element.dataset.mwaiFilterValue, localContext) : "";
                                                const filterMatches = selected == null || selected === "" || selected === "*" || String(selected) === String(filterValue);
                                                element.hidden = !conditionMatches || !filterMatches;
                                              });
                                              container.querySelectorAll("[data-mwai-asset]").forEach(element => {
                                                const asset = state._mwai?.assets?.[element.dataset.mwaiAsset];
                                                if (asset && element.tagName === "IMG") element.src = asset;
                                              });
                                              container.querySelectorAll("*").forEach(element => {
                                                for (const attribute of [...element.attributes]) {
                                                  if (!attribute.name.startsWith("data-mwai-attr-")) continue;
                                                  const name = attribute.name.slice("data-mwai-attr-".length);
                                                  const value = get(attribute.value, contexts.get(element) || context);
                                                  if (value == null) element.removeAttribute(name); else element.setAttribute(name, String(value));
                                                }
                                              });
                                              container.querySelectorAll("template[data-mwai-each]").forEach(template => {
                                                const values = get(template.dataset.mwaiEach, context);
                                                if (!Array.isArray(values)) return;
                                                const fragment = document.createDocumentFragment();
                                                values.forEach((value, index) => {
                                                  const clone = template.content.cloneNode(true);
                                                  const itemContext = value != null && typeof value === "object"
                                                    ? Object.assign(Object.create(value), value, { $index: index })
                                                    : { $value: value, $index: index };
                                                  clone.querySelectorAll("*").forEach(element => contexts.set(element, itemContext));
                                                  bind(clone, itemContext);
                                                  fragment.appendChild(clone);
                                                });
                                                template.replaceWith(fragment);
                                              });
                                            };
                                            bind(document);
                                            root.querySelectorAll("[data-mwai-tab-target]").forEach(button => button.addEventListener("click", () => {
                                              const group = button.closest("[data-mwai-tabs]") || root;
                                              group.querySelectorAll("[data-mwai-tab-panel]").forEach(panel => panel.hidden = panel.dataset.mwaiTabPanel !== button.dataset.mwaiTabTarget);
                                              group.querySelectorAll("[data-mwai-tab-target]").forEach(tab => tab.setAttribute("aria-selected", tab === button ? "true" : "false"));
                                            }));
                                            root.querySelectorAll("[data-mwai-model]").forEach(control => {
                                              const path = control.dataset.mwaiModel;
                                              const value = get(path);
                                              if (control.type === "checkbox") control.checked = Boolean(value); else if (value != null) control.value = value;
                                              control.addEventListener("input", () => {
                                                set(path, control.type === "checkbox" ? control.checked : control.type === "number" || control.type === "range" ? Number(control.value) : control.value);
                                                bind(root);
                                              });
                                            });
                                            root.querySelectorAll("[data-mwai-set]").forEach(button => button.addEventListener("click", () => {
                                              set(button.dataset.mwaiSet, JSON.parse(button.dataset.mwaiValue || "null"));
                                              bind(root);
                                            }));
                                            root.querySelectorAll("[data-mwai-toggle]").forEach(button => button.addEventListener("click", () => {
                                              const path = button.dataset.mwaiToggle;
                                              set(path, !get(path));
                                              bind(root);
                                            }));
                                            root.querySelectorAll("[data-mwai-reset]").forEach(button => button.addEventListener("click", () => {
                                              const componentId = button.dataset.mwaiReset;
                                              (state.interactions?.controls || [])
                                                .filter(control => control.componentId === componentId)
                                                .forEach(control => set(`interactions.state.${control.controlId}`, control.initialValue));
                                              root.querySelectorAll("[data-mwai-model]").forEach(control => {
                                                const value = get(control.dataset.mwaiModel);
                                                if (control.type === "checkbox") control.checked = Boolean(value); else if (value != null) control.value = value;
                                              });
                                              bind(root);
                                            }));
                                            root.querySelectorAll("[data-mwai-search]").forEach(input => input.addEventListener("input", () => {
                                              const selector = input.dataset.mwaiSearch;
                                              root.querySelectorAll(selector).forEach(item => item.hidden = !item.textContent.toLocaleLowerCase().includes(input.value.toLocaleLowerCase()));
                                            }));
                                            root.querySelectorAll("th[data-mwai-sort]").forEach(header => header.addEventListener("click", () => {
                                              const table = header.closest("table");
                                              const body = table?.tBodies[0];
                                              if (!body) return;
                                              const column = header.cellIndex;
                                              const direction = header.dataset.mwaiDirection === "asc" ? -1 : 1;
                                              [...body.rows].sort((a, b) => a.cells[column].textContent.localeCompare(b.cells[column].textContent, undefined, { numeric: true }) * direction).forEach(row => body.appendChild(row));
                                              header.dataset.mwaiDirection = direction === 1 ? "asc" : "desc";
                                            }));
                                            root.querySelectorAll("[data-mwai-chart]").forEach(element => {
                                              const option = get(element.dataset.mwaiChart, contexts.get(element) || state);
                                              if (!option || !window.echarts) return;
                                              const chart = window.echarts.init(element);
                                              chart.setOption(option);
                                              new ResizeObserver(() => chart.resize()).observe(element);
                                            });
                                            document.documentElement.dataset.mwaiRuntimeVersion = String(VERSION);
                                            document.documentElement.dataset.mwaiAiStudioVersion = AI_STUDIO_VERSION;
                                          })();
                                          """;
}