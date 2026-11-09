import React from "react";

/** Prevents a blank screen if a component crashes. */
export default class ErrorBoundary extends React.Component {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="container">
        <div className="banner error">
          Something went wrong. <button onClick={() => window.location.reload()}>Reload page</button>
        </div>
      </div>
    );
  }
}
