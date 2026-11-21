/*
 * Unisono catalog renderer.
 *
 * Draws a server-authored UI document (components from UnisonoUiCatalog) and talks to the host
 * through the MCP Apps postMessage protocol. It depends on nothing: no framework, no network,
 * no eval. User data is written with textContent, never innerHTML.
 */
(function (global) {
  "use strict";

  var PROTOCOL_VERSION = "2026-01-26";

  /* ------------------------------------------------------------------ *
   * MCP Apps transport                                                  *
   * ------------------------------------------------------------------ */

  function createTransport(options) {
    options = options || {};
    var nextId = 0;
    var pending = {};
    var listeners = { toolresult: [], toolinput: [] };
    var hostContext = null;
    var timeoutMs = options.timeoutMs || 20000;

    function parentWindow() {
      // The host can be supplied explicitly, which also makes the transport testable without an iframe.
      return options.parent || global.parent || global;
    }

    function send(message) {
      parentWindow().postMessage(message, "*");
    }

    function emit(name, payload) {
      (listeners[name] || []).forEach(function (listener) {
        try {
          listener(payload);
        } catch (error) {
          if (global.console) global.console.error(error);
        }
      });
    }

    function onMessage(event) {
      if (event.source !== parentWindow()) return;
      var message = event.data;
      if (!message || message.jsonrpc !== "2.0") return;

      if (message.id !== undefined && message.id !== null && pending[message.id]) {
        var entry = pending[message.id];
        delete pending[message.id];
        global.clearTimeout(entry.timer);
        if (message.error) entry.reject(new Error(message.error.message || "The MCP Apps host returned an error."));
        else entry.resolve(message.result);
        return;
      }

      if (typeof message.method !== "string") return;
      var params = message.params || {};
      if (message.method === "ui/notifications/tool-result") emit("toolresult", params);
      else if (message.method === "ui/notifications/tool-input") emit("toolinput", params);
      else if (message.method === "ui/notifications/host-context-changed") hostContext = merge(hostContext, params);
    }

    function request(method, params) {
      return new Promise(function (resolve, reject) {
        var id = ++nextId;
        pending[id] = { resolve: resolve, reject: reject };
        pending[id].timer = global.setTimeout(function () {
          if (!pending[id]) return;
          delete pending[id];
          reject(new Error("Timed out waiting for the MCP Apps host (" + method + ")."));
        }, timeoutMs);
        send({ jsonrpc: "2.0", id: id, method: method, params: params });
      });
    }

    function connect() {
      return request("ui/initialize", {
        appInfo: { name: "Unisono UI", version: "1.0.0" },
        appCapabilities: {},
        protocolVersion: PROTOCOL_VERSION
      }).then(function (result) {
        hostContext = (result && result.hostContext) || null;
        send({ jsonrpc: "2.0", method: "ui/notifications/initialized", params: {} });
        return result;
      });
    }

    function callTool(name, args) {
      return request("tools/call", { name: name, arguments: args || {} });
    }

    function inIframe() {
      var parent = parentWindow();
      return !!parent && parent !== global;
    }

    return {
      connect: connect,
      callTool: callTool,
      on: function (name, listener) {
        (listeners[name] = listeners[name] || []).push(listener);
      },
      start: function () {
        global.addEventListener("message", onMessage);
      },
      inIframe: inIframe,
      getHostContext: function () {
        return hostContext;
      }
    };
  }

  /* ------------------------------------------------------------------ *
   * Document rendering                                                  *
   * ------------------------------------------------------------------ */

  function createApp(root, documentData, transport) {
    if (!root) throw new Error("Unisono catalog: a root element is required.");
    if (!transport) throw new Error("Unisono catalog: an MCP Apps transport is required.");

    var surface = documentData && documentData.createSurface;
    if (!surface || !Array.isArray(surface.components)) throw new Error("Invalid UI document: missing createSurface.components.");

    var expectedCatalog = global.__UNISONO_CATALOG__ && global.__UNISONO_CATALOG__.id;
    if (expectedCatalog && surface.catalogId && surface.catalogId !== expectedCatalog) {
      throw new Error("This UI document uses catalog '" + surface.catalogId + "', but this renderer serves '" + expectedCatalog + "'.");
    }

    var byId = {};
    surface.components.forEach(function (component) {
      if (component && component.id) byId[component.id] = component;
    });
    if (!byId.root) throw new Error("Invalid UI document: no root component.");

    var data = clone(surface.dataModel || {});
    var state = { busy: false, canRun: true, error: "", status: "" };
    var updaters = [];
    var requiredFields = [];
    var buttons = [];
    var inputSyncers = [];
    var statusNode;
    var errorNode;

    /* -- paths and values ------------------------------------------- */

    function pointerSegments(pointer) {
      if (typeof pointer !== "string" || pointer.length === 0 || pointer.charAt(0) !== "/") return [];
      return pointer.slice(1).split("/").map(function (segment) {
        return segment.replace(/~1/g, "/").replace(/~0/g, "~");
      });
    }

    function getPath(target, pointer) {
      var segments = pointerSegments(pointer);
      var current = target;
      for (var i = 0; i < segments.length; i++) {
        if (current === null || typeof current !== "object") return undefined;
        current = current[segments[i]];
      }
      return current;
    }

    function setPath(target, pointer, value) {
      var segments = pointerSegments(pointer);
      if (!segments.length) return;
      var current = target;
      for (var i = 0; i < segments.length - 1; i++) {
        var key = segments[i];
        if (current[key] === null || typeof current[key] !== "object") current[key] = {};
        current = current[key];
      }
      current[segments[segments.length - 1]] = value;
    }

    function bindingPath(value) {
      return value && typeof value === "object" && typeof value["@path"] === "string" ? value["@path"] : null;
    }

    function resolve(value) {
      var path = bindingPath(value);
      return path ? getPath(data, path) : value;
    }

    /* -- DOM helpers ------------------------------------------------ */

    function element(tag, className) {
      var node = global.document.createElement(tag);
      if (className) node.className = className;
      return node;
    }

    function variantClass(variant) {
      if (variant === "title") return "unisono-title";
      if (variant === "caption") return "unisono-caption";
      return "unisono-body";
    }

    function cellText(value) {
      if (value === null || value === undefined) return "";
      if (typeof value === "boolean") return value ? "Yes" : "No";
      if (typeof value === "object") return JSON.stringify(value);
      return String(value);
    }

    function humanize(key) {
      var text = String(key).replace(/[_-]+/g, " ").replace(/([a-z0-9])([A-Z])/g, "$1 $2").trim();
      return text ? text.charAt(0).toUpperCase() + text.slice(1).toLowerCase() : String(key);
    }

    /* -- shell ------------------------------------------------------ */

    function build() {
      root.textContent = "";
      statusNode = element("p", "unisono-status");
      errorNode = element("p", "unisono-error");
      var tree = element("div");
      root.appendChild(statusNode);
      root.appendChild(errorNode);
      root.appendChild(tree);
      renderComponent(byId.root, tree);
      refreshStatus();
      refreshButtons();
    }

    function refreshStatus() {
      statusNode.textContent = state.status || "";
      statusNode.hidden = !state.status;
      errorNode.textContent = state.error || "";
      errorNode.hidden = !state.error;
    }

    function refreshButtons() {
      for (var i = 0; i < buttons.length; i++) buttons[i].disabled = state.busy || !state.canRun;
    }

    /* -- components ------------------------------------------------- */

    function renderComponent(component, parent) {
      if (!component || !component.component) throw new Error("Invalid component in the UI document.");
      switch (component.component) {
        case "Column": return renderContainer(component, parent, "column");
        case "Row": return renderContainer(component, parent, "row");
        case "Text": return renderText(component, parent);
        case "TextInput": return renderTextInput(component, parent);
        case "NumberInput": return renderNumberInput(component, parent);
        case "Select": return renderSelect(component, parent);
        case "Checkbox": return renderCheckbox(component, parent);
        case "Button": return renderButton(component, parent);
        case "Table": return renderTable(component, parent);
        case "List": return renderList(component, parent);
        case "Image": return renderImage(component, parent);
        case "Divider": {
          var divider = element("hr", "unisono-divider");
          divider.id = "u-" + component.id;
          parent.appendChild(divider);
          return;
        }
        default:
          throw new Error("Unknown component '" + component.component + "' (id '" + component.id + "').");
      }
    }

    function renderContainer(component, parent, kind) {
      var node = element("div", "unisono-" + kind + (component.gap ? " unisono-gap-" + component.gap : ""));
      node.id = "u-" + component.id;
      (component.children || []).forEach(function (id) {
        var child = byId[id];
        if (!child) throw new Error("Missing child component '" + id + "'.");
        renderComponent(child, node);
      });
      parent.appendChild(node);
    }

    function renderText(component, parent) {
      var node = element("p", "unisono-text " + variantClass(component.variant));
      node.id = "u-" + component.id;
      var update = function () {
        var value = resolve(component.text);
        var text = value === undefined || value === null ? "" : String(value);
        node.textContent = text;
        node.hidden = text.length === 0;
      };
      update();
      updaters.push(update);
      parent.appendChild(node);
    }

    function fieldShell(component, parent, input) {
      var wrapper = element("div", "unisono-field");
      var id = "u-" + component.id;
      input.id = id;
      var label = element("label");
      label.setAttribute("for", id);
      label.textContent = component.label || component.id;
      if (component.required) {
        input.required = true;
        requiredFields.push({ component: component, input: input, label: label.textContent });
      }
      if (component.component === "Checkbox") {
        wrapper.className += " unisono-checkbox";
        wrapper.appendChild(input);
        wrapper.appendChild(label);
      } else {
        wrapper.appendChild(label);
        wrapper.appendChild(input);
      }
      parent.appendChild(wrapper);
      return wrapper;
    }

    function bindInput(input, component, read, fallback) {
      var path = bindingPath(component.value);
      if (!path) return;
      input.addEventListener("input", function () {
        setPath(data, path, read(input));
        input.removeAttribute("aria-invalid");
      });
      inputSyncers.push(function () {
        var value = getPath(data, path);
        if (value === undefined || value === null) input.value = "";
        else if (input.type === "checkbox") input.checked = !!value;
        else input.value = String(value);
      });
      fallback();
    }

    function renderTextInput(component, parent) {
      var input = element("input");
      var format = component.format;
      input.type = format === "date" || format === "email" ? format : "text";
      if (component.placeholder) input.placeholder = component.placeholder;
      fieldShell(component, parent, input);
      bindInput(input, component, function (node) { return node.value; }, function () {
        var path = bindingPath(component.value);
        var value = path ? getPath(data, path) : resolve(component.value);
        if (value !== undefined && value !== null) input.value = String(value);
      });
    }

    function renderNumberInput(component, parent) {
      var input = element("input");
      input.type = "number";
      if (component.min !== undefined && component.min !== null) input.min = String(component.min);
      if (component.max !== undefined && component.max !== null) input.max = String(component.max);
      if (component.step !== undefined && component.step !== null) input.step = String(component.step);
      fieldShell(component, parent, input);
      bindInput(input, component, function (node) {
        return node.value === "" ? null : Number(node.value);
      }, function () {
        var path = bindingPath(component.value);
        var value = path ? getPath(data, path) : resolve(component.value);
        if (value !== undefined && value !== null) input.value = String(value);
      });
    }

    function renderSelect(component, parent) {
      var select = element("select");
      var options = component.options || [];
      if (!component.required) {
        var empty = element("option");
        empty.value = "";
        empty.textContent = "—";
        select.appendChild(empty);
      }
      options.forEach(function (option) {
        var node = element("option");
        node.value = option.value;
        node.textContent = option.label;
        select.appendChild(node);
      });
      fieldShell(component, parent, select);
      bindInput(select, component, function (node) { return node.value; }, function () {
        var path = bindingPath(component.value);
        var value = path ? getPath(data, path) : resolve(component.value);
        if (value !== undefined && value !== null) select.value = String(value);
      });
    }

    function renderCheckbox(component, parent) {
      var input = element("input");
      input.type = "checkbox";
      fieldShell(component, parent, input);
      bindInput(input, component, function (node) { return node.checked; }, function () {
        var path = bindingPath(component.value);
        var value = path ? getPath(data, path) : resolve(component.value);
        input.checked = !!value;
      });
    }

    function renderButton(component, parent) {
      var button = element("button", "unisono-button");
      button.id = "u-" + component.id;
      button.type = "button";
      button.setAttribute("data-variant", component.variant || "primary");
      button.textContent = component.label || "Run";
      button.addEventListener("click", function () {
        submit(component.action);
      });
      buttons.push(button);
      button.disabled = state.busy || !state.canRun;
      parent.appendChild(button);
    }

    function renderTable(component, parent) {
      var wrapper = element("div");
      wrapper.id = "u-" + component.id;
      var table = element("table", "unisono-table");
      var head = element("thead");
      var body = element("tbody");
      table.appendChild(head);
      table.appendChild(body);
      wrapper.appendChild(table);
      parent.appendChild(wrapper);

      var update = function () {
        var rows = resolve(component.rows);
        if (!Array.isArray(rows)) rows = [];
        var columns = component.columns && component.columns.length
          ? component.columns
          : deriveColumns(rows);
        head.textContent = "";
        body.textContent = "";
        wrapper.hidden = rows.length === 0 || columns.length === 0;
        if (wrapper.hidden) return;

        var headRow = element("tr");
        columns.forEach(function (column) {
          var cell = element("th");
          cell.textContent = column.label;
          headRow.appendChild(cell);
        });
        head.appendChild(headRow);

        rows.slice(0, 200).forEach(function (row) {
          var tableRow = element("tr");
          columns.forEach(function (column) {
            var cell = element("td");
            cell.textContent = cellText(row ? row[column.key] : undefined);
            tableRow.appendChild(cell);
          });
          body.appendChild(tableRow);
        });
      };

      update();
      updaters.push(update);
    }

    function deriveColumns(rows) {
      var keys = [];
      rows.slice(0, 50).forEach(function (row) {
        if (!row || typeof row !== "object" || Array.isArray(row)) return;
        Object.keys(row).forEach(function (key) {
          if (keys.indexOf(key) === -1) keys.push(key);
        });
      });
      return keys.slice(0, 8).map(function (key) {
        return { key: key, label: humanize(key) };
      });
    }

    function renderList(component, parent) {
      var list = element("ul", "unisono-list");
      list.id = "u-" + component.id;
      parent.appendChild(list);
      var update = function () {
        var items = resolve(component.items);
        if (!Array.isArray(items)) items = [];
        list.textContent = "";
        list.hidden = items.length === 0;
        items.slice(0, 200).forEach(function (item) {
          var entry = element("li");
          if (item && typeof item === "object" && !Array.isArray(item)) {
            var title = component.titleField ? item[component.titleField] : undefined;
            var subtitle = component.subtitleField ? item[component.subtitleField] : undefined;
            entry.textContent = title === undefined || title === null ? "" : String(title);
            if (subtitle !== undefined && subtitle !== null && subtitle !== "") {
              var secondary = element("span", "unisono-list-subtitle");
              secondary.textContent = String(subtitle);
              entry.appendChild(secondary);
            }
          } else {
            entry.textContent = cellText(item);
          }
          list.appendChild(entry);
        });
      };
      update();
      updaters.push(update);
    }

    function renderImage(component, parent) {
      var image = element("img", "unisono-image");
      image.id = "u-" + component.id;
      image.alt = component.alt || "";
      if (component.src) image.src = component.src;
      image.hidden = !component.src;
      parent.appendChild(image);
    }

    /* -- actions and results ---------------------------------------- */

    function submit(action) {
      if (state.busy || !action) return;
      state.error = "";

      var missing = [];
      requiredFields.forEach(function (field) {
        var path = bindingPath(field.component.value);
        var value = path ? getPath(data, path) : resolve(field.component.value);
        if (value === undefined || value === null || value === "") {
          missing.push(field.label);
          field.input.setAttribute("aria-invalid", "true");
        } else {
          field.input.removeAttribute("aria-invalid");
        }
      });

      if (missing.length) {
        state.error = "Please fill in: " + missing.join(", ") + ".";
        refreshStatus();
        return;
      }

      if (!state.canRun) {
        state.error = "This UI needs an MCP Apps host. Open it from ChatGPT or Claude instead of a plain browser tab.";
        refreshStatus();
        return;
      }

      var args = {};
      if (action.arguments) {
        Object.keys(action.arguments).forEach(function (key) {
          var value = resolve(action.arguments[key]);
          if (value !== undefined) args[key] = value;
        });
      }

      state.busy = true;
      state.status = "Calling " + action.name + "…";
      refreshStatus();
      refreshButtons();

      Promise.resolve()
        .then(function () {
          return transport.callTool(action.name, args);
        })
        .then(function (result) {
          state.status = "";
          ingestToolResult(result);
        })
        .catch(function (error) {
          state.error = error && error.message ? error.message : String(error);
        })
        .then(function () {
          state.busy = false;
          refreshStatus();
          refreshButtons();
        });
    }

    function firstText(result) {
      var content = result && result.content;
      if (!Array.isArray(content)) return null;
      var parts = [];
      content.forEach(function (part) {
        if (part && part.type === "text" && typeof part.text === "string") parts.push(part.text);
      });
      return parts.length ? parts.join("\n") : null;
    }

    function tryParseJson(text) {
      if (typeof text !== "string") return undefined;
      var trimmed = text.trim();
      if (!trimmed) return undefined;
      var first = trimmed.charAt(0);
      if (first !== "{" && first !== "[" && first !== "\"" && first !== "-" && !/[0-9tfn]/.test(first)) return undefined;
      try {
        return JSON.parse(trimmed);
      } catch (error) {
        return undefined;
      }
    }

    function ingestToolResult(result) {
      if (!result) return;
      if (result.isError) {
        state.error = firstText(result) || "The tool reported an error.";
        refreshStatus();
        return;
      }

      var payload;
      if (result.structuredContent !== undefined && result.structuredContent !== null) {
        payload = result.structuredContent;
      } else {
        var text = firstText(result);
        var parsed = tryParseJson(text);
        payload = parsed === undefined ? text : parsed;
      }

      applyResult(payload);
      state.error = "";
      refreshStatus();
    }

    function applyResult(payload) {
      var summary = { text: "", rows: [] };

      if (payload === null || payload === undefined) {
        summary.text = "";
      } else if (Array.isArray(payload)) {
        var primitives = payload.every(function (item) {
          return item === null || typeof item !== "object";
        });
        if (primitives) summary.text = payload.map(cellText).join(", ");
        else summary.rows = payload;
      } else if (typeof payload === "object") {
        var keys = Object.keys(payload);
        var simple = keys.length > 0 && keys.length <= 8 && keys.every(function (key) {
          var value = payload[key];
          return value === null || typeof value !== "object";
        });
        if (simple) {
          summary.rows = keys.map(function (key) {
            return { field: humanize(key), value: payload[key] };
          });
        } else {
          summary.text = JSON.stringify(payload, null, 2);
        }
      } else {
        summary.text = String(payload);
      }

      setPath(data, "/_result", summary);
      updaters.forEach(function (update) {
        update();
      });
    }

    function ingestToolInput(params) {
      var args = params && params.arguments;
      if (!args || typeof args !== "object") return;
      Object.keys(args).forEach(function (key) {
        data[key] = args[key];
      });
      inputSyncers.forEach(function (sync) {
        sync();
      });
    }

    /* -- lifecycle -------------------------------------------------- */

    build();

    return {
      start: function () {
        transport.on("toolresult", ingestToolResult);
        transport.on("toolinput", ingestToolInput);

        var inIframe = typeof transport.inIframe === "function" ? transport.inIframe() : true;
        if (!inIframe) {
          state.canRun = false;
          state.status = "Preview mode — open this UI from an MCP Apps host to call the tool.";
          refreshStatus();
          refreshButtons();
          return Promise.resolve(false);
        }

        state.status = "Connecting to the MCP Apps host…";
        refreshStatus();
        return Promise.resolve()
          .then(function () {
            return transport.connect();
          })
          .then(function () {
            state.canRun = true;
            state.status = "";
            refreshStatus();
            refreshButtons();
            return true;
          })
          .catch(function (error) {
            state.canRun = false;
            state.status = "MCP Apps host unavailable: " + (error && error.message ? error.message : String(error));
            refreshStatus();
            refreshButtons();
            return false;
          });
      },
      submit: submit,
      ingestToolResult: ingestToolResult,
      ingestToolInput: ingestToolInput,
      getData: function () {
        return data;
      }
    };
  }

  function clone(value) {
    return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
  }

  function merge(target, update) {
    var result = {};
    var key;
    for (key in target || {}) result[key] = target[key];
    for (key in update || {}) result[key] = update[key];
    return result;
  }

  global.UnisonoCatalog = {
    protocolVersion: PROTOCOL_VERSION,
    createTransport: createTransport,
    createApp: createApp
  };
})(typeof window !== "undefined" ? window : this);
