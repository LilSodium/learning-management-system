// Web client for the CLI.LMS API. Plain JavaScript, no build step: the
// API serves this folder (wwwroot) at "/", so the page and the API share an
// origin and every call below goes to the same server.
"use strict";

const CLASSIFICATIONS = ["Freshman", "Sophomore", "Junior", "Senior"];

// Course card colors, assigned in course-code order so courses don't repeat.
const COURSE_COLORS = ["#0B6E99", "#8F3E97", "#C1531D", "#127A3C", "#3E4FA3", "#9C6B0A"];

const main = document.getElementById("main");
const state = { courses: [] };

// ---------- helpers ----------

const esc = (value) => String(value ?? "")
  .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
  .replace(/"/g, "&quot;").replace(/'/g, "&#39;");

const enc = encodeURIComponent;

function courseColor(id) {
  const ordered = [...state.courses].sort((a, b) => String(a.code).localeCompare(String(b.code)) || String(a.id).localeCompare(String(b.id)));
  const index = ordered.findIndex((c) => c.id === id);
  if (index >= 0) return COURSE_COLORS[index % COURSE_COLORS.length];
  let hash = 0;
  for (const ch of String(id)) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  return COURSE_COLORS[hash % COURSE_COLORS.length];
}

function initials(name) {
  return String(name).split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join("");
}

function plural(n, one, many = one + "s") {
  return `${n} ${n === 1 ? one : many}`;
}

// Newest term first; fall sorts after spring of the same year.
function termKey(term) {
  const m = /^(Spring|Summer|Fall)\s+(\d{4})$/i.exec(term || "");
  if (!m) return -1;
  return Number(m[2]) * 10 + { spring: 1, summer: 2, fall: 3 }[m[1].toLowerCase()];
}

// Due dates are stored as text like "9/11/2026".
function parseDue(text) {
  const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(text || "");
  return m ? new Date(Number(m[3]), Number(m[1]) - 1, Number(m[2])) : null;
}

function formatDue(text) {
  const d = parseDue(text);
  return d ? d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }) : text || "No due date";
}

class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

async function api(path, options = {}) {
  const init = { method: options.method || "GET", headers: { Accept: "application/json" } };
  if (options.body !== undefined) {
    init.headers["Content-Type"] = "application/json";
    init.body = JSON.stringify(options.body);
  }
  let response;
  try {
    response = await fetch(path, init);
  } catch {
    throw new ApiError(0, "Can't reach the server. Check your connection and try again.");
  }
  if (response.status === 429) {
    throw new ApiError(429, "This demo allows 60 requests a minute per visitor. Wait a moment and try again.");
  }
  if (!response.ok) throw new ApiError(response.status, await errorMessage(response));
  if (response.status === 204) return null;
  return (response.headers.get("Content-Type") || "").includes("json") ? response.json() : null;
}

// The API answers errors as plain text ("Name is required.") or as
// ASP.NET problem details JSON for model validation.
async function errorMessage(response) {
  const text = await response.text();
  try {
    const problem = JSON.parse(text);
    if (problem && typeof problem === "object") {
      if (problem.errors) return Object.values(problem.errors).flat().join(" ");
      return problem.detail || problem.title || `Request failed (${response.status}).`;
    }
    if (typeof problem === "string") return problem;
  } catch { /* not JSON */ }
  return text || `Request failed (${response.status}).`;
}

let flashTimer;
function flash(message, isError = false) {
  const el = document.getElementById("flash");
  el.textContent = message;
  el.classList.toggle("error", isError);
  el.classList.add("show");
  clearTimeout(flashTimer);
  flashTimer = setTimeout(() => el.classList.remove("show"), isError ? 5000 : 2500);
}

// ---------- icons ----------

const ICONS = {
  assignment: `<path d="M7 3.5h7l4 4v13H7z"/><path d="M14 3.5v4h4M10 12h5M10 15.5h5"/>`,
  quiz: `<path d="M5 4h14v16H5z"/><path d="M8.5 9l1.5 1.5 3-3M8.5 15l1.5 1.5 3-3M15 9.5h1M15 15.5h1"/>`,
  modules: `<path d="M4 5h16M4 12h16M4 19h16"/><circle cx="4" cy="5" r=".6"/><circle cx="4" cy="12" r=".6"/>`,
  people: `<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20c0-3.6 2.9-6 6.5-6s6.5 2.4 6.5 6"/><circle cx="17" cy="9" r="2.5"/><path d="M16 14.2c3 0 5.5 2 5.5 5.3"/>`,
  page: `<path d="M6 3.5h8l4 4v13H6z"/><path d="M14 3.5v4h4M9 12h6M9 15.5h6"/>`,
  file: `<path d="M15.5 7.5 9 14a1.8 1.8 0 0 0 2.5 2.5l7-7a3.5 3.5 0 0 0-5-5L6.5 11.5a5.3 5.3 0 0 0 7.5 7.5l5-5"/>`,
  megaphone: `<path d="M4 10v4h3l7 4V6L7 10z"/><path d="M17.5 9a4 4 0 0 1 0 6"/>`,
  grade: `<path d="M5 19V9M10 19V5M15 19v-7M20 19v-4"/>`,
  plus: `<path d="M12 5v14M5 12h14"/>`,
};
const icon = (name, cls = "") => `<svg class="${cls}" viewBox="0 0 24 24" aria-hidden="true">${ICONS[name]}</svg>`;

// ---------- dialog ----------

const dialog = document.getElementById("dialog");
const dialogForm = document.getElementById("dialog-form");
let dialogHandler = null;

// onSubmit gets the form's values; throwing keeps the dialog open with
// the error shown.
function openDialog({ title, body, submit, danger = false, onSubmit }) {
  document.getElementById("dialog-title").textContent = title;
  document.getElementById("dialog-body").innerHTML = body;
  document.getElementById("dialog-error").textContent = "";
  const submitBtn = document.getElementById("dialog-submit");
  submitBtn.textContent = submit;
  submitBtn.className = danger ? "btn btn-danger" : "btn btn-primary";
  submitBtn.disabled = false;
  dialogHandler = onSubmit;
  dialog.showModal();
  const first = dialog.querySelector("#dialog-body input:not([readonly]), #dialog-body select, #dialog-body textarea");
  (first || submitBtn).focus();
}

dialogForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!dialogHandler) return;
  const submitBtn = document.getElementById("dialog-submit");
  const values = Object.fromEntries(new FormData(dialogForm).entries());
  submitBtn.disabled = true;
  try {
    await dialogHandler(values);
    dialog.close();
  } catch (err) {
    document.getElementById("dialog-error").textContent = err.message;
  } finally {
    submitBtn.disabled = false;
  }
});
dialog.addEventListener("click", (event) => {
  if (event.target.closest("[data-close]")) dialog.close();
});

// ---------- global nav (and the mobile drawer) ----------

const nav = document.getElementById("global-nav");
const scrim = document.getElementById("nav-scrim");
const menuBtn = document.getElementById("menu-btn");

function setDrawer(open) {
  nav.classList.toggle("open", open);
  scrim.hidden = !open;
  menuBtn.setAttribute("aria-expanded", String(open));
}
menuBtn.addEventListener("click", () => setDrawer(!nav.classList.contains("open")));
scrim.addEventListener("click", () => setDrawer(false));
nav.addEventListener("click", (event) => { if (event.target.closest("a")) setDrawer(false); });

async function refreshCourses() {
  state.courses = await api("/api/courses");
  return state.courses;
}

// ---------- routing ----------

function currentRoute() {
  const hash = location.hash.replace(/^#/, "") || "/";
  const course = /^\/courses\/([^/]+)(?:\/(assignments|modules|people))?$/.exec(hash);
  if (course) return { name: "course", id: decodeURIComponent(course[1]), tab: course[2] || "home" };
  if (hash === "/courses") return { name: "courses" };
  if (hash === "/students") return { name: "students" };
  return { name: "dashboard" };
}

function sameRoute(a, b) {
  return a.name === b.name && a.id === b.id && a.tab === b.tab;
}

function markNav(route) {
  const key = route.name === "course" ? "courses" : route.name;
  nav.querySelectorAll("[data-nav]").forEach((a) => {
    if (a.dataset.nav === key) a.setAttribute("aria-current", "page");
    else a.removeAttribute("aria-current");
  });
}

function setTitle(text) {
  document.title = `${text} · CLI LMS`;
  document.getElementById("mobile-title").textContent = text;
}

async function render() {
  const route = currentRoute();
  markNav(route);
  try {
    if (route.name === "course") await renderCourse(route);
    else if (route.name === "courses") await renderAllCourses(route);
    else if (route.name === "students") await renderStudents(route);
    else await renderDashboard(route);
  } catch (err) {
    if (sameRoute(route, currentRoute())) renderError(err);
  }
}

// A view that finishes after the visitor moved on must not draw over the
// page they navigated to.
function stillOn(route) {
  return sameRoute(route, currentRoute());
}

function crumbs(items, actions = "") {
  return `
    <nav class="crumbs" aria-label="Breadcrumb">
      <ol>${items.map(([label, href], i) => `<li>${href && i < items.length - 1 ? `<a href="${href}">${esc(label)}</a>` : `<span${i === items.length - 1 ? ' aria-current="page"' : ""}>${esc(label)}</span>`}</li>`).join("")}</ol>
      ${actions ? `<div class="actions">${actions}</div>` : ""}
    </nav>`;
}

function renderError(err) {
  setTitle("Page not found");
  main.innerHTML = `
    ${crumbs([["Dashboard", "#/"], ["Error"]])}
    <div class="content">
      <div class="notice" role="alert">
        <p>${esc(err.status === 404 ? "That page doesn't exist. The course may have been deleted." : err.message)}</p>
        <a class="btn" href="#/">Go to the dashboard</a>
      </div>
    </div>`;
}

window.addEventListener("hashchange", async () => {
  await render();
  main.focus({ preventScroll: true });
  window.scrollTo(0, 0);
});

// ---------- dashboard ----------

async function renderDashboard(route) {
  const courses = await refreshCourses();
  const works = await Promise.all(courses.map((c) => api(`/api/courses/${enc(c.id)}/coursework`).catch(() => null)));
  if (!stillOn(route)) return;
  setTitle("Dashboard");

  // Newest term on the dashboard, like a "current enrollments" view.
  const newest = Math.max(...courses.map((c) => termKey(c.term)), -1);
  const current = courses.filter((c) => termKey(c.term) === newest || newest === -1)
    .sort((a, b) => a.code.localeCompare(b.code));
  const older = courses.length - current.length;

  const toGrade = [];
  courses.forEach((c, i) => (works[i]?.assignments || []).forEach((a) => {
    const waiting = a.submissionCount - a.gradedCount;
    if (waiting > 0) toGrade.push({ course: c, a, waiting });
  }));
  toGrade.sort((x, y) => (parseDue(x.a.dueDate) || 0) - (parseDue(y.a.dueDate) || 0));

  main.innerHTML = `
    ${crumbs([["Dashboard"]], `<button class="btn btn-primary" type="button" data-action="new-course">${icon("plus")}Course</button>`)}
    <div class="content wide with-aside">
      <section aria-labelledby="dash-h">
        <h1 id="dash-h" class="visually-hidden">Dashboard</h1>
        ${current.length === 0 ? `<p class="empty">No courses yet. Use the Course button to create one.</p>` : `
          <div class="card-grid">${current.map(courseCard).join("")}</div>`}
        ${older > 0 ? `<p class="empty" style="margin-top:20px">${plural(older, "course")} from earlier terms. <a href="#/courses">View all courses</a></p>` : ""}
      </section>
      <aside class="todo" aria-labelledby="todo-h">
        <h2 id="todo-h">To Do</h2>
        ${toGrade.length === 0 ? `<p class="empty">Nothing to grade.</p>` : `<ul>${toGrade.map(({ course, a, waiting }) => `
          <li style="--c:${courseColor(course.id)}">${icon(a.isQuiz ? "quiz" : "assignment")}
            <span><a class="what" href="#/courses/${enc(course.id)}/assignments">Grade ${esc(a.name)}</a>
            <span class="meta">${esc(course.code)} · ${plural(waiting, "submission")} to grade</span></span>
          </li>`).join("")}</ul>`}
      </aside>
    </div>`;
}

function courseCard(c) {
  const base = `#/courses/${enc(c.id)}`;
  return `
    <div class="course-card" style="--c:${courseColor(c.id)}">
      <a class="hero" href="${base}" tabindex="-1" aria-hidden="true"></a>
      <div class="info">
        <a class="name" href="${base}" title="${esc(c.name)}">${esc(c.name)}</a>
        <div class="code">${esc(c.code)}</div>
        <div class="term">${esc(c.term)}</div>
      </div>
      <div class="links">
        <a href="${base}/assignments" title="Assignments" aria-label="${esc(c.code)} assignments">${icon("assignment")}</a>
        <a href="${base}/modules" title="Modules" aria-label="${esc(c.code)} modules">${icon("modules")}</a>
        <a href="${base}/people" title="People" aria-label="${esc(c.code)} people">${icon("people")}</a>
      </div>
    </div>`;
}

// ---------- all courses ----------

async function renderAllCourses(route) {
  const courses = await refreshCourses();
  if (!stillOn(route)) return;
  setTitle("All courses");
  const sorted = [...courses].sort((a, b) => termKey(b.term) - termKey(a.term) || a.code.localeCompare(b.code));
  main.innerHTML = `
    ${crumbs([["Dashboard", "#/"], ["All courses"]], `<button class="btn btn-primary" type="button" data-action="new-course">${icon("plus")}Course</button>`)}
    <div class="content">
      <div class="page-title"><h1>All courses</h1></div>
      ${sorted.length === 0 ? `<p class="empty">No courses yet.</p>` : `
      <div class="table-wrap"><table>
        <thead><tr><th scope="col">Course</th><th scope="col">Code</th><th scope="col">Term</th><th scope="col" class="right">Students</th></tr></thead>
        <tbody>${sorted.map((c) => `<tr>
          <td><a href="#/courses/${enc(c.id)}">${esc(c.name)}</a></td>
          <td>${esc(c.code)}</td>
          <td>${esc(c.term)}</td>
          <td class="right">${c.rosterCount}</td>
        </tr>`).join("")}</tbody>
      </table></div>`}
    </div>`;
}

// ---------- course pages ----------

const TABS = [["home", "Home", ""], ["assignments", "Assignments", "/assignments"], ["modules", "Modules", "/modules"], ["people", "People", "/people"]];

async function renderCourse(route) {
  const id = route.id;
  const needWork = route.tab !== "people";
  const needRoster = route.tab === "people" || route.tab === "home";
  const [course, work, roster, students] = await Promise.all([
    api(`/api/courses/${enc(id)}`),
    needWork ? api(`/api/courses/${enc(id)}/coursework`) : null,
    needRoster ? api(`/api/courses/${enc(id)}/roster`) : null,
    route.tab === "people" ? api("/api/students") : null,
    state.courses.length ? null : refreshCourses(),
  ]);
  if (!stillOn(route)) return;

  const tabLabel = TABS.find((t) => t[0] === route.tab)[1];
  setTitle(route.tab === "home" ? course.code : `${tabLabel}: ${course.code}`);
  const base = `#/courses/${enc(course.id)}`;

  let body;
  if (route.tab === "assignments") body = assignmentsPage(course, work);
  else if (route.tab === "modules") body = modulesPage(course, work);
  else if (route.tab === "people") body = peoplePage(course, roster, students);
  else body = homePage(course, work, roster);

  main.innerHTML = `
    ${crumbs([[course.code, base], [tabLabel]])}
    <div class="with-menu">
      <nav class="course-menu" aria-label="Course">
        <p class="term">${esc(course.term)}</p>
        <ul>${TABS.map(([key, label, path]) => `<li><a href="${base}${path}"${key === route.tab ? ' aria-current="page"' : ""}>${label}</a></li>`).join("")}</ul>
      </nav>
      <div class="content">${body}</div>
    </div>`;

  wireCoursePage(course, route);
}

function homePage(course, work, roster) {
  const graded = work.assignments.reduce((n, a) => n + a.gradedCount, 0);
  const submitted = work.assignments.reduce((n, a) => n + a.submissionCount, 0);
  const upcoming = [...work.assignments].sort((a, b) => (parseDue(a.dueDate) || 0) - (parseDue(b.dueDate) || 0));
  return `
    <div class="page-title">
      <div>
        <h1>${esc(course.name)}</h1>
        <p class="sub">${esc(course.code)} · ${esc(course.term)}</p>
      </div>
      <div class="actions">
        <button class="btn" type="button" data-action="edit-course">Edit</button>
        <button class="btn" type="button" data-action="delete-course">Delete</button>
      </div>
    </div>
    <div class="with-aside">
      <div>
        ${course.announcement ? `
          <section class="announcement" aria-labelledby="ann-h">
            <p class="label" id="ann-h">${icon("megaphone")}Announcement</p>
            <p>${esc(course.announcement)}</p>
          </section>` : ""}
        <h2 class="section">About this course</h2>
        <div class="prose"><p>${course.description ? esc(course.description) : `<span class="empty">No description yet.</span>`}</p></div>
        <h2 class="section">Assignments by due date</h2>
        ${upcoming.length === 0 ? `<p class="empty">No assignments posted.</p>` : `
          <div class="item-group"><ul class="items">${upcoming.map((a) => assignmentItem(course, a)).join("")}</ul></div>`}
      </div>
      <aside class="todo" aria-labelledby="status-h">
        <h2 id="status-h">Course status</h2>
        <ul>
          <li>${icon("people")}<span><a class="what" href="#/courses/${enc(course.id)}/people">${plural(roster.length, "student")}</a><span class="meta">enrolled</span></span></li>
          <li>${icon("assignment")}<span><a class="what" href="#/courses/${enc(course.id)}/assignments">${plural(work.assignments.length, "assignment")}</a><span class="meta">posted</span></span></li>
          <li>${icon("grade")}<span><span class="what">${graded} of ${submitted} graded</span><span class="meta">submissions</span></span></li>
        </ul>
      </aside>
    </div>`;
}

function assignmentItem(course, a) {
  const allGraded = a.submissionCount > 0 && a.gradedCount === a.submissionCount;
  return `
    <li class="${a.averageGrade != null ? "has-score" : ""}">
      ${icon(a.isQuiz ? "quiz" : "assignment", "icon")}
      <div class="main">
        <span class="title">${esc(a.name)}</span>${a.isQuiz ? `<span class="pill">Quiz</span>` : ""}${allGraded ? `<span class="pill ok">Graded</span>` : ""}
        <div class="details">
          <span><b>Due</b> ${esc(formatDue(a.dueDate))}</span>
          <span>${a.availablePoints} pts</span>
          <span>${a.submissionCount === 0 ? "No submissions" : `${a.gradedCount}/${a.submissionCount} graded`}</span>
        </div>
      </div>
      <div class="score">${a.averageGrade != null ? `<strong>${a.averageGrade}/${a.availablePoints}</strong>class average` : ""}</div>
    </li>`;
}

function assignmentsPage(course, work) {
  const quizzes = work.assignments.filter((a) => a.isQuiz);
  const others = work.assignments.filter((a) => !a.isQuiz);
  const total = work.assignments.reduce((n, a) => n + a.availablePoints, 0);
  const group = (title, list) => list.length === 0 ? "" : `
    <section class="item-group" aria-label="${esc(title)}">
      <div class="group-head"><span>${esc(title)}</span><span class="meta">${list.reduce((n, a) => n + a.availablePoints, 0)} pts</span></div>
      <ul class="items">${list.map((a) => assignmentItem(course, a)).join("")}</ul>
    </section>`;
  return `
    <div class="page-title">
      <div><h1>Assignments</h1><p class="sub">${plural(work.assignments.length, "assignment")} · ${total} points total</p></div>
    </div>
    ${work.assignments.length === 0 ? `<p class="empty">No assignments posted for this course.</p>` : group("Assignments", others) + group("Quizzes", quizzes)}`;
}

function modulesPage(course, work) {
  return `
    <div class="page-title"><div><h1>Modules</h1><p class="sub">${plural(work.modules.length, "module")}</p></div></div>
    ${work.modules.length === 0 ? `<p class="empty">No modules published for this course.</p>` : work.modules.map((m) => {
      const page = m.items.find((i) => i.type === "page");
      const title = page ? page.name : m.id;
      return `
        <section class="item-group" aria-label="${esc(title)}">
          <div class="group-head"><span>${esc(title)}</span><span class="meta">${plural(m.items.length, "item")}</span></div>
          <ul class="items">${m.items.map((i) => `
            <li>${icon(i.type === "file" ? "file" : "page", "icon")}
              <div class="main"><span class="title">${esc(i.type === "file" ? i.name : "Overview")}</span>
              <div class="details">${i.type === "file" ? "File" : esc(i.name)}</div></div>
            </li>`).join("")}</ul>
        </section>`;
    }).join("")}`;
}

function peoplePage(course, roster, students) {
  const enrolled = new Set(roster.map((s) => s.id));
  const available = students.filter((s) => !enrolled.has(s.id)).sort((a, b) => a.name.localeCompare(b.name));
  const sorted = [...roster].sort((a, b) => a.name.localeCompare(b.name));
  return `
    <div class="page-title"><div><h1>People</h1><p class="sub">${plural(roster.length, "student")} enrolled</p></div></div>
    ${sorted.length === 0 ? `<p class="empty">Nobody is enrolled yet. Add a student below.</p>` : `
    <div class="table-wrap"><table>
      <thead><tr><th scope="col">Name</th><th scope="col">Student code</th><th scope="col" class="hide-sm">Classification</th><th scope="col" class="right"><span class="visually-hidden">Actions</span></th></tr></thead>
      <tbody>${sorted.map((s) => `<tr>
        <td><span class="person"><span class="avatar" aria-hidden="true">${esc(initials(s.name))}</span>${esc(s.name)}</span></td>
        <td>${esc(s.code)}</td>
        <td class="hide-sm">${esc(CLASSIFICATIONS[s.classification] ?? "")}</td>
        <td class="right"><button class="btn btn-sm" type="button" data-unenroll="${esc(s.id)}" aria-label="Remove ${esc(s.name)} from ${esc(course.code)}">Remove</button></td>
      </tr>`).join("")}</tbody>
    </table></div>`}
    ${available.length ? `
      <form class="inline-form" data-enroll>
        <label class="visually-hidden" for="enroll-select">Student to add</label>
        <select id="enroll-select" name="studentId" required>
          <option value="">Choose a student to add…</option>
          ${available.map((s) => `<option value="${esc(s.id)}">${esc(s.name)} (${esc(s.code)})</option>`).join("")}
        </select>
        <button class="btn btn-primary" type="submit">Add to course</button>
      </form>` : `<p class="empty" style="margin-top:16px">Every student is enrolled in this course.</p>`}`;
}

function wireCoursePage(course, route) {
  const edit = main.querySelector('[data-action="edit-course"]');
  if (edit) edit.onclick = () => courseDialog(course);
  const del = main.querySelector('[data-action="delete-course"]');
  if (del) del.onclick = () => deleteCourseDialog(course);

  main.querySelectorAll("[data-unenroll]").forEach((btn) => {
    btn.onclick = async () => {
      btn.disabled = true;
      try {
        await api(`/api/courses/${enc(course.id)}/roster/${enc(btn.dataset.unenroll)}`, { method: "DELETE" });
        flash("Student removed from the course");
        await renderCourse(route);
      } catch (err) {
        flash(err.message, true);
        btn.disabled = false;
      }
    };
  });

  const form = main.querySelector("[data-enroll]");
  if (form) {
    form.onsubmit = async (event) => {
      event.preventDefault();
      const studentId = form.studentId.value;
      if (!studentId) return;
      const btn = form.querySelector("button");
      btn.disabled = true;
      try {
        await api(`/api/courses/${enc(course.id)}/roster/${enc(studentId)}`, { method: "POST" });
        flash("Student added to the course");
        await renderCourse(route);
        document.getElementById("enroll-select")?.focus();
      } catch (err) {
        flash(err.message, true);
        btn.disabled = false;
      }
    };
  }
}

function courseDialog(course) {
  const editing = Boolean(course);
  const c = course || { code: "", name: "", term: defaultTerm(), description: "", announcement: "" };
  openDialog({
    title: editing ? "Edit course details" : "Create a course",
    submit: editing ? "Save" : "Create course",
    body: `
      <label class="field">Course name
        <input name="name" value="${esc(c.name)}" required autocomplete="off">
      </label>
      <div class="field-row">
        <label class="field">Course code
          <input name="code" value="${esc(c.code)}" ${editing ? "readonly" : 'required placeholder="COP 4710"'} autocomplete="off">
          ${editing ? `<span class="hint">Can't be changed after creation.</span>` : ""}
        </label>
        <label class="field">Term
          <input name="term" value="${esc(c.term)}" placeholder="Fall 2026" autocomplete="off">
        </label>
      </div>
      <label class="field">Description
        <textarea name="description" rows="3">${esc(c.description)}</textarea>
      </label>
      <label class="field">Announcement
        <textarea name="announcement" rows="2">${esc(c.announcement)}</textarea>
        <span class="hint">Shown at the top of the course home page. Leave empty for none.</span>
      </label>`,
    onSubmit: async (v) => {
      if (!v.name.trim()) throw new Error("Enter a course name.");
      const body = {
        code: (v.code || "").trim(),
        name: v.name.trim(),
        term: (v.term || "").trim(),
        description: (v.description || "").trim(),
        announcement: (v.announcement || "").trim() || null,
      };
      if (editing) {
        await api(`/api/courses/${enc(course.id)}`, { method: "PUT", body: { ...body, id: course.id, code: course.code } });
        flash("Course details saved");
        await refreshCourses();
        await render();
      } else {
        if (!body.code) throw new Error("Enter a course code.");
        body.id = body.code.replace(/\s+/g, "").toUpperCase();
        const created = await api("/api/courses", { method: "POST", body });
        flash("Course created");
        await refreshCourses();
        location.hash = `#/courses/${enc(created.id)}`;
      }
    },
  });
}

function deleteCourseDialog(course) {
  openDialog({
    title: "Delete this course?",
    submit: "Delete course",
    danger: true,
    body: `<p style="margin:0">${esc(course.code)} ${esc(course.name)} will be deleted along with its modules, assignments and submissions. Enrolled students stay in the system.</p>`,
    onSubmit: async () => {
      await api(`/api/courses/${enc(course.id)}`, { method: "DELETE" });
      flash("Course deleted");
      location.hash = "#/";
    },
  });
}

function defaultTerm() {
  const now = new Date();
  const m = now.getMonth();
  return `${m >= 7 ? "Fall" : m >= 4 ? "Summer" : "Spring"} ${now.getFullYear()}`;
}

// ---------- students ----------

const studentFilter = { q: "", level: "" };

async function renderStudents(route) {
  const [students, courses] = await Promise.all([api("/api/students"), refreshCourses()]);
  const rosters = await Promise.all(courses.map((c) => api(`/api/courses/${enc(c.id)}/roster`).catch(() => [])));
  if (!stillOn(route)) return;
  setTitle("Students");

  const enrollments = new Map();
  rosters.forEach((list, i) => list.forEach((s) => {
    if (!enrollments.has(s.id)) enrollments.set(s.id, []);
    enrollments.get(s.id).push(courses[i]);
  }));

  main.innerHTML = `
    ${crumbs([["Dashboard", "#/"], ["Students"]], `<button class="btn btn-primary" type="button" data-action="new-student">${icon("plus")}Student</button>`)}
    <div class="content">
      <div class="page-title">
        <div><h1>Students</h1><p class="sub">Deleting a student removes them from every course and deletes their submissions.</p></div>
      </div>
      <div class="toolbar">
        <label class="visually-hidden" for="student-search">Search students</label>
        <input id="student-search" type="search" placeholder="Search by name or student code" value="${esc(studentFilter.q)}" autocomplete="off">
        <label class="visually-hidden" for="student-level">Classification</label>
        <select id="student-level">
          <option value="">All classifications</option>
          ${CLASSIFICATIONS.map((c, i) => `<option value="${i}" ${String(i) === studentFilter.level ? "selected" : ""}>${c}</option>`).join("")}
        </select>
        <span class="empty" id="student-count"></span>
      </div>
      <div class="table-wrap"><table>
        <thead><tr>
          <th scope="col">Name</th>
          <th scope="col">Student code</th>
          <th scope="col">Classification</th>
          <th scope="col" class="hide-sm">Courses</th>
          <th scope="col" class="right"><span class="visually-hidden">Actions</span></th>
        </tr></thead>
        <tbody id="student-rows"></tbody>
      </table></div>
    </div>`;

  const rows = document.getElementById("student-rows");
  const draw = () => {
    const q = studentFilter.q.trim().toLowerCase();
    const list = students
      .filter((s) => !q || s.name.toLowerCase().includes(q) || s.code.toLowerCase().includes(q))
      .filter((s) => studentFilter.level === "" || String(s.classification) === studentFilter.level)
      .sort((a, b) => a.name.localeCompare(b.name));
    document.getElementById("student-count").textContent = `Showing ${list.length} of ${students.length}`;
    rows.innerHTML = list.length === 0
      ? `<tr><td colspan="5" class="empty">No students match. Clear the search or add a student.</td></tr>`
      : list.map((s) => {
        const inCourses = (enrollments.get(s.id) || []).sort((a, b) => a.code.localeCompare(b.code));
        return `<tr>
          <td><span class="person"><span class="avatar" aria-hidden="true">${esc(initials(s.name))}</span>${esc(s.name)}</span></td>
          <td>${esc(s.code)}</td>
          <td>${esc(CLASSIFICATIONS[s.classification] ?? "")}</td>
          <td class="hide-sm">${inCourses.length ? inCourses.map((c) => `<a href="#/courses/${enc(c.id)}/people">${esc(c.code)}</a>`).join(", ") : `<span class="empty">None</span>`}</td>
          <td class="right"><span class="row-actions">
            <button class="btn btn-sm" type="button" data-edit="${esc(s.id)}" aria-label="Edit ${esc(s.name)}">Edit</button>
            <button class="btn btn-sm" type="button" data-delete="${esc(s.id)}" aria-label="Delete ${esc(s.name)}">Delete</button>
          </span></td>
        </tr>`;
      }).join("");
  };
  draw();

  document.getElementById("student-search").oninput = (e) => { studentFilter.q = e.target.value; draw(); };
  document.getElementById("student-level").onchange = (e) => { studentFilter.level = e.target.value; draw(); };
  rows.onclick = (event) => {
    const edit = event.target.closest("[data-edit]");
    const del = event.target.closest("[data-delete]");
    if (edit) studentDialog(students.find((s) => s.id === edit.dataset.edit));
    if (del) deleteStudentDialog(students.find((s) => s.id === del.dataset.delete));
  };
}

function studentDialog(student) {
  const editing = Boolean(student);
  const s = student || { name: "", code: "", classification: 0 };
  openDialog({
    title: editing ? "Edit student" : "Add a student",
    submit: editing ? "Save" : "Add student",
    body: `
      <label class="field">Full name
        <input name="name" value="${esc(s.name)}" required autocomplete="off">
      </label>
      <div class="field-row">
        <label class="field">Student code
          <input name="code" value="${esc(s.code)}" required placeholder="S1009" autocomplete="off">
        </label>
        <label class="field">Classification
          <select name="classification">
            ${CLASSIFICATIONS.map((c, i) => `<option value="${i}" ${i === s.classification ? "selected" : ""}>${c}</option>`).join("")}
          </select>
        </label>
      </div>`,
    onSubmit: async (v) => {
      if (!v.name.trim()) throw new Error("Enter the student's name.");
      if (!v.code.trim()) throw new Error("Enter a student code.");
      const body = { name: v.name.trim(), code: v.code.trim(), classification: Number(v.classification) };
      if (editing) {
        await api(`/api/students/${enc(student.id)}`, { method: "PUT", body: { ...body, id: student.id } });
        flash("Student saved");
      } else {
        // Seeded students use their code as their id, so new ones do too.
        await api("/api/students", { method: "POST", body: { ...body, id: body.code } });
        flash("Student added");
      }
      await render();
    },
  });
}

function deleteStudentDialog(student) {
  openDialog({
    title: "Delete this student?",
    submit: "Delete student",
    danger: true,
    body: `<p style="margin:0">${esc(student.name)} will be removed from every course roster, and their submissions will be deleted.</p>`,
    onSubmit: async () => {
      await api(`/api/students/${enc(student.id)}`, { method: "DELETE" });
      flash("Student deleted");
      await render();
    },
  });
}

// ---------- page-level actions ----------

main.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  if (action === "new-course") courseDialog(null);
  if (action === "new-student") studentDialog(null);
});

render();
