import { useCallback, useEffect, useState } from "react";
import AdminPortal from "./AdminPortal.jsx";
import GuardianPortal from "./GuardianPortal.jsx";

function useRoute() {
  const get = () => window.location.hash.replace(/^#\/?/, "");
  const [route, setRoute] = useState(get);
  useEffect(() => {
    const f = () => setRoute(get());
    window.addEventListener("hashchange", f);
    return () => window.removeEventListener("hashchange", f);
  }, []);
  return route;
}

export default function App() {
  const route = useRoute();
  const [toasts, setToasts] = useState([]);
  const notify = useCallback((type, message) => {
    const id = Date.now() + Math.random();
    setToasts((t) => [...t, { id, type, message }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 5000);
  }, []);

  let page;
  if (route === "admin") page = <AdminPortal notify={notify} />;
  else if (route === "guardian") page = <GuardianPortal notify={notify} />;
  else page = (
    <>
      <header className="hero"><div className="wrap">
        <p className="brand">Onnorokom School</p>
        <h1>Admissions, first come first served</h1>
        <p className="sub">Choose how you are using the system today.</p>
      </div></header>
      <main className="wrap">
        <div className="roles">
          <a className="role admin" href="#/admin">
            <h2>I'm a school admin</h2>
            <p>Create classes, set the number of seats, and watch admissions fill up live.</p>
            <span className="go">Open admin portal</span>
          </a>
          <a className="role guardian" href="#/guardian">
            <h2>I'm a guardian</h2>
            <p>Hold a seat for your child, then confirm the admission fee before the timer ends.</p>
            <span className="go">Open guardian portal</span>
          </a>
        </div>
      </main>
    </>
  );

  return (
    <>
      {route && <div className="topbar"><div className="wrap"><a href="#/">Onnorokom School</a><a href="#/">Switch role</a></div></div>}
      {page}
      <div className="toasts" aria-live="polite">
        {toasts.map((t) => <div key={t.id} className={`toast ${t.type}`}>{t.message}</div>)}
      </div>
    </>
  );
}
